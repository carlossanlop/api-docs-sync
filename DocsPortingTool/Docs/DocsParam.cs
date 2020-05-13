﻿using System.Xml.Linq;

namespace DocsPortingTool.Docs
{
    public class DocsParam
    {
        private readonly XElement XEDocsParam = null;
        public IDocsAPI ParentAPI
        {
            get; private set;
        }
        public string Name
        {
            get
            {
                return XmlHelper.GetAttributeValue(XEDocsParam, "name");
            }
        }
        public string Value
        {
            get
            {
                return XmlHelper.GetNodesInPlainText(XEDocsParam);
            }
            set
            {
                XmlHelper.SaveFormattedAsXml(XEDocsParam, value);
                ParentAPI.Changed = true;
            }
        }
        public DocsParam(IDocsAPI parentAPI, XElement xeDocsParam)
        {
            ParentAPI = parentAPI;
            XEDocsParam = xeDocsParam;
        }
        public override string ToString()
        {
            return Name;
        }
    }
}