using Microsoft.Practices.ServiceLocation;
using SolrNet.Impl;
using SolrNet.Impl.FieldSerializers;
using SolrNet.Impl.QuerySerializers;
using System;

namespace Sitecore.ContentSearch.Linq.Solr
{
  internal static class SolrQuerySerializerUtility
  {
    public static string Serialize(object query)
    {
      return GetQuerySerializer().Serialize(query);
    }

    public static ISolrQuerySerializer GetQuerySerializer()
    {
      ISolrQuerySerializer solrQuerySerializer = null;
      try
      {
        IServiceLocator current = ServiceLocator.Current;
        solrQuerySerializer = ServiceLocator.Current.GetInstance<ISolrQuerySerializer>();
      }
      catch (NullReferenceException)
      {
      }
      if (solrQuerySerializer == null)
      {
        solrQuerySerializer = new DefaultQuerySerializer(new DefaultFieldSerializer());
      }
      return solrQuerySerializer;
    }
  }
}