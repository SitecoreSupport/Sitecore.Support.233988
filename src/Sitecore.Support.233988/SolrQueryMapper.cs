using Sitecore.Abstractions;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.ContentSearch.Linq.Common;
using Sitecore.ContentSearch.Linq.Helpers;
using Sitecore.ContentSearch.Linq.Nodes;
using Sitecore.ContentSearch.Linq.Solr;
using Sitecore.ContentSearch.Pipelines.FormatQueryFieldValue;
using SolrNet;
using SolrNet.Impl;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Sitecore.Support.ContentSearch.Linq.Solr
{
  public class SolrQueryMapper : Sitecore.ContentSearch.Linq.Solr.SolrQueryMapper
  {
    private readonly IFieldQueryTranslatorMap<IFieldQueryTranslator> fieldQueryTranslators;

    public SolrQueryMapper(SolrIndexParameters parameters) : base(parameters)
    {
      fieldQueryTranslators = Parameters.FieldQueryTranslators;
    }

    private object ConvertDateTimeToUtc(object objectToConvert, string fieldName)
    {
      if (objectToConvert != null && objectToConvert is DateTime)
      {
        DateTime serverTime = (DateTime)objectToConvert;
        if (serverTime.Kind != DateTimeKind.Utc && Parameters.ConvertQueryDatesToUtc)
        {
          return ContentSearchManager.Locator.GetInstance<IDateTimeConverter>().ToUniversalTime(serverTime);
        }
        return objectToConvert;
      }
      return objectToConvert;
    }

    protected override AbstractSolrQuery Visit(QueryNode node, Sitecore.ContentSearch.Linq.Solr.SolrQueryMapper.SolrQueryMapperState state)
    {
      switch (node.NodeType)
      {
        case QueryNodeType.Contains:
          return this.VisitContains((ContainsNode)node, state);
        case QueryNodeType.EndsWith:
          return this.VisitEndsWith((EndsWithNode)node, state);
        case QueryNodeType.StartsWith:
          return this.VisitStartsWith((StartsWithNode)node, state);
      }
      return base.Visit(node, state);
    }

    protected override AbstractSolrQuery VisitContains(ContainsNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.Contains, state, out result))
      {
        return result;
      }
      return VisitContains(fieldNode.FieldKey, valueNode.Value, node.Boost);
    }

    protected override AbstractSolrQuery VisitContains(string field, object value, float boost)
    {
      field = field.ToLowerInvariant();
      base.ValueFormatter.FormatValueForIndexStorage(value, field);
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = field,
        FieldValue = value.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(field.Replace(" ", "_"), "*" + formatQueryFieldValueArgs.FieldValue + "*")
      {
        Quoted = false//formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }

    protected override AbstractSolrQuery VisitEndsWith(EndsWithNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      string text = fieldNode.FieldKey.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(valueNode.Value, text);
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = text,
        FieldValue = obj.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(text.Replace(" ", "_"), "*" + formatQueryFieldValueArgs.FieldValue)
      {
        Quoted = false//formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(node.Boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)node.Boost);
      }
      return abstractSolrQuery;
    }

    protected override AbstractSolrQuery VisitStartsWith(StartsWithNode node, SolrQueryMapperState state)
    {
      FieldNode fieldNode = QueryHelper.GetFieldNode(node);
      ConstantNode valueNode = QueryHelper.GetValueNode<string>((BinaryNode)node);
      AbstractSolrQuery result;
      if (ProcessAsVirtualField(fieldNode, valueNode, node.Boost, ComparisonType.StartsWith, state, out result))
      {
        return result;
      }
      return this.VisitStartsWith(fieldNode.FieldKey, valueNode.Value, node.Boost);
    }

    protected override AbstractSolrQuery VisitStartsWith(string field, object value, float boost)
    {
      field = field.ToLowerInvariant();
      object obj = base.ValueFormatter.FormatValueForIndexStorage(value, field);
      obj = new Regex("^\\/", RegexOptions.Compiled).Replace(obj.ToString(), "\\/");
      FormatQueryFieldValueArgs formatQueryFieldValueArgs = new FormatQueryFieldValueArgs
      {
        FieldName = field,
        FieldValue = obj.ToString(),
        IsQuoted = false,
        Translator = FieldNameTranslator,
        ExcludeEscapeCharacters = new Collection<string>
                {
                    "\\",
                    "/"
                },
        SuppressIsQuotedRule = (fieldQueryTranslators.GetTranslator(field.ToLowerInvariant()) != null)
      };
      RunFormatQueryFieldValuePipeline(formatQueryFieldValueArgs);
      AbstractSolrQuery abstractSolrQuery = new SolrQueryByField(field.Replace(" ", "_"), formatQueryFieldValueArgs.FieldValue + "*")
      {
        Quoted = false//formatQueryFieldValueArgs.IsQuoted
      };
      if (Math.Abs(boost - 1f) > 1.401298E-45f)
      {
        abstractSolrQuery = abstractSolrQuery.Boost((double)boost);
      }
      return abstractSolrQuery;
    }
  }
}