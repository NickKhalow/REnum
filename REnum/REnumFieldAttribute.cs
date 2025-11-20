using System;

namespace REnum
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class REnumFieldAttribute : Attribute
    {
        public Type FieldType { get; }
        public string? CustomName { get; }

        public REnumFieldAttribute(Type fieldType, string? customName = null)
        {
            FieldType = fieldType;
            CustomName = customName;
        }
    }
}