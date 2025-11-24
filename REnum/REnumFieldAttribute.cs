using System;

namespace REnum
{
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
    public class REnumFieldAttribute : Attribute
    {
        public Type FieldType { get; }
        public string? CustomName { get; }
        public bool Nullable { get; }

        public REnumFieldAttribute(Type fieldType, string? customName = null, bool nullable = false)
        {
            FieldType = fieldType;
            CustomName = customName;
            Nullable = nullable;
        }
    }
}