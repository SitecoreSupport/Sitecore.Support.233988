using Microsoft.Practices.ServiceLocation;
using SolrNet.Impl;
using SolrNet.Impl.FieldSerializers;
using SolrNet.Impl.QuerySerializers;
using System;

namespace Sitecore.ContentSearch.Linq.Solr
{
  internal static class SolrQuerySerializerUtility
  {
    public static ISolrQuerySerializer GetQuerySerializer()
    {
      ISolrQuerySerializer instance = null;
      try
      {
        IServiceLocator current = ServiceLocator.Current;
        instance = ServiceLocator.Current.GetInstance<ISolrQuerySerializer>();
      }
      catch (NullReferenceException)
      {
      }
      if (instance == null)
      {
        instance = new DefaultQuerySerializer(new DefaultFieldSerializer());
      }
      return instance;
    }

    public static string Serialize(object query) =>
        GetQuerySerializer().Serialize(query);
  }
}