﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using SM64_Diagnostic.Utilities;
using SM64_Diagnostic.Structs;
using SM64_Diagnostic.Extensions;
using System.Reflection;
using SM64_Diagnostic.Managers;
using SM64_Diagnostic.Structs.Configurations;

namespace SM64_Diagnostic.Controls
{
    public class AddressHolderLock
    {
        public readonly bool IsSpecial;
        public readonly Type MemoryType;
        public readonly int? ByteCount;
        public readonly uint? Mask;
        public readonly uint? EffectiveAddress;
        public readonly string SpecialType;
        public readonly Func<string, bool> SetterFunction;

        private string _value;
        public string Value { get { return _value; } }

        public AddressHolderLock(
            bool isSpecial,
            Type memoryType,
            int? byteCount,
            uint? mask,
            uint? effectiveAddress,
            string specialType,
            Func<string, bool> setterFunction,
            string value)
        {
            IsSpecial = isSpecial;
            MemoryType = memoryType;
            ByteCount = byteCount;
            Mask = mask;
            EffectiveAddress = effectiveAddress;
            SpecialType = specialType;
            SetterFunction = setterFunction;

            _value = value;
        }

        public void Invoke()
        {
            SetterFunction(_value);
        }

        public void UpdateLockValue(string value)
        {
            _value = value;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is AddressHolderLock)) return false;
            AddressHolderLock other = (AddressHolderLock)obj;
            return this.IsSpecial == other.IsSpecial &&
                   this.MemoryType == other.MemoryType &&
                   this.ByteCount == other.ByteCount &&
                   this.Mask == other.Mask &&
                   this.EffectiveAddress == other.EffectiveAddress &&
                   this.SpecialType == other.SpecialType;
        }

    }
}
