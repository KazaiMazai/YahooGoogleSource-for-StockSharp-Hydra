using Yahoo;

namespace StockSharp.Hydra.Yahoo
{
    using System;
    using System.Collections.Generic;

    using Ecng.Common;
    using Ecng.Collections;

    using StockSharp.Algo.History;
    using StockSharp.Algo.History.Finam;
    using StockSharp.BusinessEntities;
    using StockSharp.Hydra.Core;

    class YahooGoogleSecurityStorage : ISecurityStorage
    {
        private readonly ISecurityStorage _underlyingStorage;
        private readonly SynchronizedDictionary<string, Security> _cacheByYahooId = new SynchronizedDictionary<string, Security>();

        public IEnumerable<Security> YahooSecurities { get { return _cacheByYahooId.Values; } }

        public YahooGoogleSecurityStorage(ISecurityStorage underlyingStorage, HydraEntityRegistry entityRegistry)
        {
            if (underlyingStorage == null)
                throw new ArgumentNullException("underlyingStorage");

            

            foreach (var security in entityRegistry.Securities)
                TryAddToCache(security);

            _underlyingStorage = underlyingStorage;
        }

        public Security LoadBy(string fieldName, object fieldValue)
        {
            if (fieldName.CompareIgnoreCase("Id"))
                return _underlyingStorage.LoadBy(fieldName, fieldValue);

            return _cacheByYahooId.TryGetValue((string)fieldValue);
        }

        public IEnumerable<Security> Lookup(Security criteria)
        {
            return new[] { criteria };

        }

        public void Save(Security security)
        {
            _underlyingStorage.Save(security);
            TryAddToCache(security);
        }

        private void TryAddToCache(Security security)
        {
            if (security == null)
                throw new ArgumentNullException("security");

            var yahooId = security.ExtensionInfo.TryGetValue(YahooGoogelHistorySource.YahooSecurityIdField);

            if (yahooId != null)
                _cacheByYahooId.SafeAdd((string)yahooId, key => security);
        }

        
         
    }
}
