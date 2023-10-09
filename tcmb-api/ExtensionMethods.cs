using System.Xml;

namespace tcmb_api
{
    public static class ExtensionMethods
    {
        public static string GetCurrencyValueFromXml(this XmlDocument xml, string name)
        {
            return xml.GetElementsByTagName(name).Cast<XmlNode>().Last(l => l.HasChildNodes).InnerText;
        }
    }
}
