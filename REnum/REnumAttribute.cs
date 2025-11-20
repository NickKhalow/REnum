using System;

namespace REnum
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class REnumAttribute : Attribute
    {
        public EnumUnderlyingType UnderlyingType { get; }

        public REnumAttribute(EnumUnderlyingType underlyingType = EnumUnderlyingType.Int)
        {
            UnderlyingType = underlyingType;
        }
    }

}