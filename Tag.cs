using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PLCLib
{

    public enum tType : byte
    {
        InputType = 1,
        FlagType = 2,
        TimerType = 3,
        OutputType = 4,
        CounterType = 5,
        RegisterType = 6,
        NumberType = 7,
        ASCIIType = 8,
        FloatType = 19,
        SystemType = 20,
        StringType = 22
    };
    /// <summary>
    /// Maps to a tag value on WinPLC
    /// </summary>
    public class Tag
    {
        public tType type;
        public Int16 tagNumber;
        public string name;
        public byte Tl; // tagNumber Low byte
        public byte Th; // tagNumber High byte

        public byte[] ByteValue = new byte[4];
        public string StringValue = "";

        /// <summary>
        /// Initializes a new instance of Tag.
        /// </summary>
        /// <param name="tagType">Type of tag (same as in PLC).</param>
        /// <param name="tagNumber">Tag number (same as in PLC).</param>
        /// <param name="tagName">Tag name (same as in PLC).</param>
        public Tag(tType tagType, Int16 tagNumber, string tagName)
        {
            this.type = tagType;
            this.tagNumber = tagNumber;
            this.name = tagName;
            Tl = (byte)tagNumber;
            Th = (byte)((tagNumber & 0xFF00) >> 8);
        }

        /// <summary>
        /// Returns value based on tag type.
        /// </summary>
        /// <returns></returns>
        public object getValue()
        {
            switch (type)
            {
                case tType.InputType:
                case tType.FlagType:
                case tType.OutputType:
                    return BitConverter.ToBoolean(ByteValue, 0);
                case tType.TimerType:
                    return BitConverter.ToUInt32(ByteValue, 0);
                case tType.CounterType:
                    return BitConverter.ToInt16(ByteValue, 0);
                case tType.NumberType:
                    return BitConverter.ToInt32(ByteValue, 0);
                case tType.FloatType:
                    return BitConverter.ToSingle(ByteValue, 0);
                case tType.StringType:
                    return StringValue;
                default:
                    return null;
            }
        }

    }
}
