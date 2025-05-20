using System;

namespace REnum
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class REnumFieldEmptyAttribute : Attribute
    {
        public string FieldName { get; }

        public REnumFieldEmptyAttribute(string fieldName)
        {
            FieldName = fieldName;
        }
    }
}