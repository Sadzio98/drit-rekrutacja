using System;
using System.Linq;
using System.Collections.Generic;
using Soneta.Business;
using Soneta.Business.Db;
using Soneta.Business.UI;
using Soneta.Kadry;
using Soneta.Tools;
using Soneta.Types;
using Rekrutacja.Workers;

[assembly: Worker(typeof(CalculatorWorker), typeof(Pracownicy))]
namespace Rekrutacja.Workers
{
    public sealed class CalculatorWorker
    {
        private const string DataObliczenFeatureName = "DataObliczen";

        private const string WynikFeatureName = "Wynik";

        [Context]
        public Context Cx { get; set; }

        [Context]
        public CalculatorWorkerParametry Parametry { get; set; }

        [Context]
        public IEnumerable<Pracownik> Pracownicy { get; set; } = Enumerable.Empty<Pracownik>();

        [Action("Kalkulator",
           Description = "Prosty kalkulator ",
           Priority = 10,
           Mode = ActionMode.ReadOnlySession,
           Icon = ActionIcon.Accept,
           Target = ActionTarget.ToolbarWithText)]
        public object WykonajAkcje()
        {
            CheckFeatures();

            if (!Pracownicy.Any())
            {
                return new MessageBoxInformation("Błąd") { Text = "Należy wskazać przynajmniej jednego pracownika." };
            }

            if (Parametry.Operacja == Operacja.Dzielenie && Parametry.ZmiennaY == 0)
            {
                return new MessageBoxInformation("Błąd") { Text = "Nie można dzielić przez 0!" };
            }

            double wynik = 0;

            switch (Parametry.Operacja)
            {
                case Operacja.Dodawanie:
                    wynik = Parametry.ZmiennaX + Parametry.ZmiennaY;
                    break;

                case Operacja.Odejmowanie:
                    wynik = Parametry.ZmiennaX - Parametry.ZmiennaY;
                    break;

                case Operacja.Mnozenie:
                    wynik = Parametry.ZmiennaX * Parametry.ZmiennaY;
                    break;

                case Operacja.Dzielenie:
                    wynik = Parametry.ZmiennaX / Parametry.ZmiennaY;
                    break;

                default: return new MessageBoxInformation("Błąd") { Text = "Wybrana operacja nie jest obsługiwana." };
            }

            try
            {
                using (Session session = Cx.Login.CreateSession(false, true, $"ModyfikacjaPracownikow"))
                {
                    using (ITransaction trans = session.Logout(true))
                    {
                        foreach (var pracownik in Pracownicy)
                        {
                            var pracownikZSesja = session.Get(pracownik);

                            pracownikZSesja.Features[DataObliczenFeatureName] = Parametry.DataObliczen;
                            pracownikZSesja.Features[WynikFeatureName] = wynik;

                            trans.CommitUI();
                        }
                    }

                    session.Save();
                }
                return new MessageBoxInformation("Sukces") { Text = "Operacja zakończona pomyślnie." };
            }
            catch (Exception ex)
            {
                return new MessageBoxInformation("Błąd") { Text = $"Operacja zakończona niepowodzeniem: {ex.Message}" };
            }
        }

        private void CheckFeatures()
        {
            FeatureDefinitions features;
            using (Session session = Cx.Login.CreateSession(false, true, $"WeryfikacjaCech"))
            {
                features = KadryModule.GetInstance(session).Pracownicy.FeatureDefinitions;
            }

            var dataObliczenFeatureExist = features.Contains(DataObliczenFeatureName);
            var wynikFeatureExist = features.Contains(WynikFeatureName);

            if (!dataObliczenFeatureExist)
            {
                CreateFeature(DataObliczenFeatureName, FeatureTypeNumber.Date);
            }

            if (!wynikFeatureExist)
            {
                CreateFeature(WynikFeatureName, FeatureTypeNumber.Double);
            }
        }

        private void CreateFeature(string featureName, FeatureTypeNumber typeNumber)
        {
            using (Session configSession = Cx.Login.CreateSession(false, true, $"InstalacjaCechy{featureName}"))
            {
                using (ITransaction tran = configSession.Logout(true))
                {
                    var featureDefinition = new FeatureDefinition(nameof(Pracownicy))
                    {
                        Name = featureName,
                        TypeNumber = typeNumber
                    };

                    configSession.GetBusiness().FeatureDefs.AddRow(featureDefinition);

                    tran.Commit();
                }

                configSession.Save();
            }
        }

        public sealed class CalculatorWorkerParametry : ContextBase
        {
            [Caption("Zmienna X")]
            [Priority(10)]
            public double ZmiennaX { get; set; }

            [Priority(20)]
            [Caption("Operacja")]
            public Operacja Operacja { get; set; }

            [Priority(30)]
            [Caption("Zmienna Y")]
            public double ZmiennaY { get; set; }

            [Caption("Data obliczeń")]
            [Priority(40)]
            public Date DataObliczen { get; set; }

            public CalculatorWorkerParametry(Context context) : base(context)
            {
                DataObliczen = Date.Today;
            }
        }

        public enum Operacja
        {
            [Caption("+")]
            Dodawanie,

            [Caption("-")]
            Odejmowanie,

            [Caption("*")]
            Mnozenie,

            [Caption("/")]
            Dzielenie
        }
    }
}