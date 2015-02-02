using System;

namespace DataLayer.Attributes
{
    public class ExtensionAttribute : Attribute
    {
        public string Extension { get; set; }

        public ExtensionAttribute(string extension)
        {
            Extension = extension;
        }
    }
}
