﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Diagnostics.Runtime;
using LiveSplit.EscapeGoat2.Debugging;

namespace LiveSplit.EscapeGoat2.Memory
{
    public struct ValuePointer
    {
        public readonly ulong Address;
        public readonly ClrType Type;
        public readonly ProcessMangler Mangler;

        public ValuePointer(ClrRoot r, ProcessMangler mangler) {
            // r.Address is the memory location of the root, not the thing it points to
            r.Type.Heap.ReadPointer(r.Address, out Address);
            Type = r.Type;
            Mangler = mangler;
        }

        public ValuePointer(ulong address, ClrType type, ProcessMangler mangler) {
            if (type == null)
                throw new ArgumentNullException("type");

            Address = address;
            Type = type;
            Mangler = mangler;
        }

        public ValuePointer? this[string fieldName] {
            get {
                var field = Type.GetFieldByName(fieldName);
                if (field == null)
                    throw new Exception(String.Format("No field with this name '{0}'", fieldName));

                ulong address;

                if (field.IsObjectReference()) {
                    var fieldValue = field.GetValue(Address);
                    if (fieldValue == null)
                        return null;

                    address = (ulong)fieldValue;
                } else {
                    address = field.GetAddress(Address, false);
                }

                if (address == 0)
                    return null;

                return new ValuePointer(address, field.Type, this.Mangler);
            }
        }

        public T GetFieldValue<T>(string fieldName) {
            var field = Type.GetFieldByName(fieldName);
            if (field == null)
                throw new Exception(String.Format("No field with this name '{0}'", fieldName));
            return (T)Convert.ChangeType(field.GetValue(Address), typeof(T));
        }

        public ValuePointer ForceCast(ClrType newType) {
            return new ValuePointer(Address, newType, this.Mangler);
        }

        public ValuePointer ForceCast(string newTypeName) {
            var newType = Mangler.GetTypeByName(newTypeName);
            if (newType == null)
                throw new Exception("No type with this name");

            return ForceCast(newType);
        }

        public void EnumerateReferences(Action<ulong, int> action) {
            Type.EnumerateRefsOfObjectCarefully(Address, action);
        }

        public object Read() {
            ulong address = Address;
            // This is required due to a bug in Microsoft.Diagnostics.Runtime
            if (Type.IsPrimitive) {
                address -= (ulong)((long)this.Mangler.Heap.PointerSize);
            }

            return Type.GetValue(address);
        }

        public T ReadValue<T>() where T : new() {
            FieldInfo[] fields = typeof(T).GetFields();

            T item = new T();
            foreach (var field in Type.Fields) {
                if (!field.HasSimpleValue) continue;

                for (int j = 0; j < fields.Length; j++) {
                    if (fields[j].Name == field.Name) {
                        var val = field.GetValue(Address, true);
                        typeof(T).GetField(field.Name).SetValueDirect(__makeref(item), val);
                    }
                }
            }
            return item;
        }

        public T Read<T>() {
            object res = Read();
            if (res == null) {
                return default(T);
            }
            return (T)Convert.ChangeType(res, typeof(T));
        }

        public override string ToString() {
            return String.Format("<{0:X8} {1}>", Address, Type.Name);
        }
    }
}
