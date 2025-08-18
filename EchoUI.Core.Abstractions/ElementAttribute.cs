using System;
using System.Collections.Generic;
using System.Text;

namespace EchoUI.Core.Abstractions
{

    [AttributeUsageAttribute(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ElementAttribute : Attribute
    {
        public string? DefaultProperty { get; set; }
    }
}
