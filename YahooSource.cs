using Ecng.Collections;
using StockSharp.Algo.History;
using StockSharp.Hydra.Yahoo;
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
    using StockSharp.Logging;
    using StockSharp.BusinessEntities;
    using StockSharp.Hydra.Core;
    using StockSharp.Algo.Storages;

    class YahooSource : BaseMarketDataSource, ISecuritySource
    {
        [DisplayName("Настройки источника Yahoo")]
        private  sealed class YahooSettings : RealMarketDataSourceSettings
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
            [Description("C какого года начать скачивать данные.")]
            public DateTime StartFrom
			{
				get { return ExtensionInfo["StartFrom"].To<DateTime>(); }
				set { ExtensionInfo["StartFrom"] = value; }
			}
		 

            [Category("Yahoo")]
            [DisplayName("Путь к файлу символов")]
            [Description("Файл символов инструментов для загрузки")]
            public string SymbolsFile
            {
                get { return (string)ExtensionInfo["SymbolsFile"]; }
                set { ExtensionInfo["SymbolsFile"] = value; }
            }
        }

        private YahooSettings _settings;
        private YahooHistorySource _source;
        private IEnumerable<Security> _selectedSecurities;
        private YahooSecurityStorage _yahooSecurityStorage;

        public YahooSource()
            : base("CAFCA9BD-8D2A-4194-B2BF-364DAFABFFB6".To<Guid>())
        {
        }

        protected override void ApplySettings(MarketDataSourceSettings settings, bool isNew)
        {
            _settings = new YahooSettings(settings);

            if (isNew)
            {
                _settings.YahooOffset = 1;
                _settings.StartFrom = new DateTime(2000,1,1);
                _settings.SymbolsFile = "\\symbols.txt";
            }
            
             _yahooSecurityStorage = new YahooSecurityStorage(SecurityStorage, EntityRegistry);
              _source = new YahooHistorySource(_yahooSecurityStorage) { DumpFolder = GetTempPath() };
              if (!EntityRegistry.Exchanges.Any(e => e.Name == YahooHistorySource.UsMarket.Name)) EntityRegistry.Exchanges.Save(YahooHistorySource.UsMarket);
        }

        public override Uri Icon
        {
            get { return "yahoo.png".GetResourceUrl(GetType()); }
        }

        public override MarketDataSourceSettings Settings
        {
            get { return _settings; }
        }

        public override string Name
        {
            get { return "Yahoo"; }
        }

        public override string Description
        {
            get { return "Источник предназначен для получения рыночных данных с сайта Yahoo."; }
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
 

        
        public override void Load()
        {
            if (this.GetAllSecurity() != null) _selectedSecurities = _yahooSecurityStorage.YahooSecurities;
            else _selectedSecurities = Securities;

            foreach (var security in _selectedSecurities)
          
            {
                if (ProcessState != ProcessStates.Started)
                {
                    this.AddInfoLog("Прерывание загрузки данных.");
                    return;
                }

                const string key = "LastCandleTime";
               var  date = _settings.StartFrom;
                if (security.ExtensionInfo.ContainsKey(key))
                {
                    if (security.ExtensionInfo[key].To<DateTime>()+TimeSpan.FromDays(1) > date)
                        date = security.ExtensionInfo[key].To<DateTime>()+ TimeSpan.FromDays(1);
                  
                }
                    
             
                if (RedownloadMode)
                {
                    date = _settings.StartFrom;
                    
                }

                if (date  < DateTime.Today &&  
                    !(DateTime.Now.Hour<6 && (date+ TimeSpan.FromDays(1)==DateTime.Today) )  &&
                    !(DateTime.Now.Hour<6 && (date+ TimeSpan.FromDays(3)==DateTime.Today) && date.DayOfWeek==DayOfWeek.Saturday && DateTime.Today.DayOfWeek== DayOfWeek.Tuesday) &&
                    !( (DateTime.Today -date).Days<=2 &&        date.DayOfWeek == DayOfWeek.Saturday && (DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday || DateTime.Now.DayOfWeek == DayOfWeek.Monday)))
               
                { 
                 
                    //если для инструмента указаны периоды свечек и этот источник
                    if (security.ContainsSource(this) && !security.GetCandlePeriods().IsEmpty())
                    {
                        foreach (var period  in security.GetCandlePeriods() )
                        {
                            this.AddInfoLog("Старт загрузки {0} свечек с {1} по {2} для {3}.", period, date.ToShortDateString(), DateTime.Now.ToShortDateString(), security.Id);

                         
                           var candles =  _source.GetCandles(security, date, DateTime.Today, period);
                           if (!candles.IsEmpty())
                           {
                               if (RedownloadMode)
                                   foreach (var candle in candles)
                                   {
                                       DeleteCandles(security, period, candle.OpenTime);
                                   }
                               
                               SaveCandles(security, period, candles);
                               DateTime lastDate = candles.Max(c => c.OpenTime);
                               DateTime startDate = candles.Min(c => c.OpenTime);
                               this.AddInfoLog("Сохранение свечек с {0} по {1} для {2}", startDate, lastDate, security.Id);

                               if (security.ExtensionInfo.ContainsKey(key))
                                   security.ExtensionInfo[key] = lastDate.To<long>();
                               else security.ExtensionInfo.Add(key, lastDate.To<long>());
                               EntityRegistry.Securities.Save(security);
                           }
                           else
                           {
                               this.AddInfoLog("Новые свечки для {0} не загружены.", security.Id);

                           }

                        }
                        
                    }

                   

                    
                }
                 
                
             
            }
 
        }
         
        public IEnumerable<Security> GetNewSecurities( )
        {
          var securities =  _source.GetNewSecurities(_settings.SymbolsFile);
            var timeframe = new TimeSpan(1, 0, 0, 0);
            foreach (var security in securities)
            {
                security.SetCandlePeriods(new[] {timeframe});
                security.AddSource(this);
                EntityRegistry.Securities.Save(security);
                
            }

            return securities;




        }

        

      
    }

    
}
