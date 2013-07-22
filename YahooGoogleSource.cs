using Ecng.Collections;
using StockSharp.Algo.History;
using StockSharp.Hydra.Yahoo;
using StockSharp.Logging;
using Yahoo;

namespace StockSharp.Hydra
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using Ecng.Collections;
    using Ecng.Common;
    using Ecng.Xaml;
    using StockSharp.Algo;
    using StockSharp.Algo.Candles;
    using StockSharp.Algo.History.Finam;
 
    using StockSharp.BusinessEntities;
    using StockSharp.Hydra.Core;
    using StockSharp.Algo.Storages;
    class YahooGoogleSource : BaseMarketDataSource, ISecuritySource
    {
        [DisplayName("Настройки источника Yahoo-Google EoD")]
        private sealed class YahooSettings : MarketDataSourceSettings
        {
            public YahooSettings(MarketDataSourceSettings settings)
                : base(settings)
            {
            }

            [Category("Yahoo")]
            [DisplayName("Временной отступ")]
            [Description("Временной отступ в днях от текущей даты, который необходим для предотвращения скачивания неполных данных за текущую торговую сессию.")]
            public int YahooOffset
            {
                get { return ExtensionInfo["FinamOffset"].To<int>(); }
                set { ExtensionInfo["FinamOffset"] = value; }
            }
           
           
            [Category("Yahoo")]
            [DisplayName("Начальная дата")]
            [Description("Начальная дата")]
            public DateTime StartFrom
			{
				get { return ExtensionInfo["StartFrom"].To<DateTime>(); }
				set { ExtensionInfo["StartFrom"] = value; }
			}
		 

            [Category("Yahoo")]
            [DisplayName("Путь к файлу символов Nasdaq")]
            [Description("Файл символов инструментов для загрузки")]
            public string NasdaqSymbolsFile
            {
                get { return (string)ExtensionInfo["NasdaqSymbolsFile"]; }
                set { ExtensionInfo["NasdaqSymbolsFile"] = value; }
            }

            [Category("Yahoo")]
            [DisplayName("Путь к файлу символов NYSE")]
            [Description("Файл символов инструментов для загрузки")]
            public string NYSESymbolsFile
            {
                get { return (string)ExtensionInfo["NYSESymbolsFile"]; }
                set { ExtensionInfo["NYSESymbolsFile"] = value; }
            }
            [Category("Yahoo")]
            [DisplayName("Загружать инструменты NASDAQ")]
            [Description("Загружать инструменты NASDAQ")]
            public bool UseNASDAQ
            {
                get { return (bool)ExtensionInfo["UseNASDAQ"]; }
                set { ExtensionInfo["UseNASDAQ"] = value; }
            }
            [Category("Yahoo")]
            [DisplayName("Загружать инструменты NYSE")]
            [Description("Загружать инструменты NYSE")]
            public bool UseNYSE
            {
                get { return (bool)ExtensionInfo["UseNYSE"]; }
                set { ExtensionInfo["UseNYSE"] = value; }
            }

        }

        private YahooSettings _settings;
        private YahooGoogelHistorySource _source;
        private IEnumerable<Security> _selectedSecurities;
        private YahooGoogleSecurityStorage _yahooGoogleSecurityStorage;

       
        protected override void ApplySettings(MarketDataSourceSettings settings, bool isNew)
        {
            _settings = new YahooSettings(settings);

            if (isNew)
            {
                _settings.YahooOffset = 1;
                _settings.StartFrom = new DateTime(2000,1,1);
                _settings.NasdaqSymbolsFile = "\\nasdaqTickers.txt";
                _settings.NYSESymbolsFile = "\\nyseTickers.txt";
                _settings.UseNASDAQ = true;
                _settings.UseNYSE = false;
            }
            
             _yahooGoogleSecurityStorage = new YahooGoogleSecurityStorage(SecurityStorage, EntityRegistry);
              _source = new YahooGoogelHistorySource(_yahooGoogleSecurityStorage) { DumpFolder = GetTempPath() };
           //   if (!EntityRegistry.Exchanges.Any(e => e.Name == YahooGoogelHistorySource.UsMarket.Name)) EntityRegistry.Exchanges.Save(YahooGoogelHistorySource.UsMarket);
        }

        public override void Load()
        {

            if (this.GetAllSecurity() != null) _selectedSecurities = _yahooGoogleSecurityStorage.YahooSecurities;
            else _selectedSecurities = Securities;

            var startDate = _settings.StartFrom;
            var endDate = DateTime.Today - TimeSpan.FromDays(_settings.YahooOffset);

            var allDates = new List<DateTime>();
             for (; startDate <= endDate; startDate = startDate.AddDays(1))
            {
                allDates.Add(startDate);
            }
             foreach (var security in _selectedSecurities)
            {
                if (ProcessState!= SourceProcessStates.Started)
                {
                    this.AddInfoLog("Прерывание загрузки данных.");
                    return;
                }

              
                LoadCandles(security, allDates);
            }
           
        }


        private void LoadCandles(Security security, List<DateTime> allDates)
        {
             var periods = security.GetCandlePeriods();

            if (security.ContainsSource<Candle>(this) && !periods.IsEmpty())
            {
                foreach (var period in periods)
                {
                    if (ProcessState != SourceProcessStates.Started)
                    {
                        this.AddInfoLog("Прерывание загрузки данных.");
                        return;
                    }

                    var storage = StorageRegistry.GetCandleStorage(new CandleSeries(typeof(TimeFrameCandle), security, period), GetDrive());
                    var emptyDates = allDates.Except(storage.Dates).ToArray();

                    if (emptyDates.IsEmpty())
                    {
                        this.AddInfoLog("Нет несохраненных дней для загрузки {0} свечек по {1}.", period, security.Id);
                        continue;
                    }

                    foreach (var emptyDate in emptyDates)
                    {
                        if (ProcessState != SourceProcessStates.Started)
                        {
                            this.AddInfoLog("Прерывание загрузки данных.");
                            return;
                        }

                        this.AddInfoLog("Старт загрузки {0} свечек за {1} для {2}.", period, emptyDate.ToShortDateString(), security.Id);

                        var candles = _source.GetCandles(security, emptyDate, emptyDate, period);
                        SaveCandles(security, period, candles);
                    }
                }
            }
        }



        public override Uri Icon
        {
            get { return "yahoo.png".GetResourceUrl(GetType()); }
        }

        public override MarketDataSourceSettings Settings
        {
            get { return _settings; }
        }

        public string Name
        {
            get { return "Yahoo+Google"; }
        }

        public override string Description
        {
            get { return "Источник предназначен для получения EoD данных с Yahoo и Google."; }
        }

        public override Type[] SupportedDataTypes
        {
            get { return new[] { typeof(Candle) }; }
        }

        public override bool IsSupportHistoricalData { get { return true; } }

        public override void SaveSettings()
        {
            base.SaveSettings();

            if (_source != null)
                _source.DumpFolder = GetTempPath();
        }

        

        private string GetTempPath()
        {
            var tempPath = Path.Combine(_settings.StorageFolder, "TemporaryFiles");

            if (!Directory.Exists(tempPath))
                Directory.CreateDirectory(tempPath);

            return tempPath;
        }
        public   void SetCandlePeriods(  Security security, IEnumerable<TimeSpan> periods)
        {
            if (security == null)
                throw new ArgumentNullException("security");

            if (periods == null)
                throw new ArgumentNullException("periods");

            security.ExtensionInfo["CandlePeriods"] = periods.Select(p => p.To<long>()).ToArray();
        }

       
        public IEnumerable<Security> GetNewSecurities( )
        {

            this.AddDebugLog("GetNewSecurities()");
            EntityRegistry.ExchangeBoards.Save(_source.GetSmartExchangeBoard());

            var securities = new List<Security>();

            if (_settings.UseNASDAQ)
            if(_settings.NasdaqSymbolsFile.Length>0)
            securities.AddRange(  _source.GetNASDAQNewSecurities(_settings.NasdaqSymbolsFile));
            
            if(_settings.UseNYSE)
            if (_settings.NYSESymbolsFile.Length > 0)
                securities.AddRange(_source.GetNYSENewSecurities(_settings.NYSESymbolsFile));

            var timeframe = TimeSpan.FromDays(1);
            foreach (var security in securities)
            {
               

                SetCandlePeriods(security, new[] {timeframe});
                security.AddSource(typeof (Candle), this);
                EntityRegistry.Securities.Save(security);
                
            }

            return securities;




        }


        
    }

    
}
