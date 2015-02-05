using System;

namespace DataLayer.Attributes
{
    public class ExtensionAttribute : Attribute
    {
        public string[] Extensions { get; set; }

        public ExtensionAttribute(string extension)
        {
            Extensions = new[] { extension };
        }

        public ExtensionAttribute(params string[] extensions)
        {
            Extensions = extensions;
        }
    }
}
