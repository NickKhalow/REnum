using System;

namespace REnum
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class REnumFieldAttribute : Attribute
    {
        public Type FieldType { get; }

        public REnumFieldAttribute(Type fieldType)
        {
            FieldType = fieldType;
        }
    }
}