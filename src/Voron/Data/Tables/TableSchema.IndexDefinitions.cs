using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Sparrow;
using Sparrow.Binary;
using Sparrow.Server;
using Voron.Impl;

namespace Voron.Data.Tables
{
    public unsafe partial class TableSchema
    {
        [Flags]
        public enum TreeIndexType
        {
            Default = 0x01,
            DynamicKeyValues = 0x2
        }

        public abstract class AbstractTreeIndexDef
        {
            public abstract TreeIndexType Type { get; }

            public bool IsGlobal;

            public Slice Name;
        }

        public class IndexDef : AbstractTreeIndexDef
        {
            public override TreeIndexType Type => TreeIndexType.Default;

            /// <summary>
            /// Here we take advantage on the fact that the values are laid out in memory sequentially
            /// we can point to a certain item index, and use one or more fields in the key directly, 
            /// without any copying
            /// </summary>
            public int StartIndex = -1;

            public int Count = -1;

            public ByteStringContext.Scope GetValue(ByteStringContext context, ref TableValueReader value,
                out Slice slice)
            {
                var ptr = value.Read(StartIndex, out int totalSize);
#if DEBUG
                if (totalSize < 0)
                    throw new ArgumentOutOfRangeException(nameof(totalSize), "Size cannot be negative");
#endif
                for (var i = 1; i < Count; i++)
                {
                    int size;
                    value.Read(i + StartIndex, out size);
#if DEBUG
                    if (size < 0)
                        throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative");
#endif
                    totalSize += size;
                }
#if DEBUG
                if (totalSize < 0 || totalSize > value.Size)
                    throw new ArgumentOutOfRangeException(nameof(value), "Reading a slice that is longer than the value");
                if (totalSize > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(totalSize),
                        "Reading a slice that too big to be a slice");
#endif
                return Slice.External(context, ptr, (ushort)totalSize, out slice);
            }

            public ByteStringContext.Scope GetValue(ByteStringContext context, TableValueBuilder value,
                out Slice slice)
            {
                if (Count == 1)
                    return value.SliceFromLocation(context, StartIndex, out slice);

                int totalSize = value.SizeOf(StartIndex);
                for (int i = 1; i < Count; i++)
                {
                    totalSize += value.SizeOf(i + StartIndex);
                }
#if DEBUG
                if (totalSize < 0)
                    throw new ArgumentOutOfRangeException(nameof(totalSize), "Size cannot be negative");
#endif
                var scope = context.Allocate(totalSize, out ByteString ret);
                try
                {
                    var ptr = ret.Ptr;
                    Slice val;
                    using (value.SliceFromLocation(context, StartIndex, out val))
                    {
                        val.CopyTo(ptr);
                        ptr += val.Size;
                    }
                    for (var i = 1; i < Count; i++)
                    {
                        using (value.SliceFromLocation(context, i + StartIndex, out val))
                        {
                            val.CopyTo(ptr);
                            ptr += val.Size;
                        }
                    }
                    slice = new Slice(ret);
                    return scope;
                }
                catch (Exception)
                {
                    scope.Dispose();
                    throw;
                }
            }

            public byte[] Serialize()
            {
                // We serialize the Type enum as ulong to be "future-proof"
                var castedType = (long)Type;

                var serializer = new TableValueBuilder
                {
                    castedType,
                    StartIndex,
                    Count,
                    IsGlobal,
                    Name
                };

                byte[] serialized = new byte[serializer.Size];

                fixed (byte* destination = serialized)
                {
                    serializer.CopyTo(destination);
                }

                return serialized;
            }

            public void EnsureIdentical(IndexDef actual)
            {
                if (actual == null)
                    throw new ArgumentNullException(nameof(actual), "Expected an index but received null");

                if (!SliceComparer.Equals(Name, actual.Name))
                    throw new ArgumentException(
                        $"Expected index to have Name='{Name}', got Name='{actual.Name}' instead",
                        nameof(actual));

                if (IsGlobal != actual.IsGlobal)
                    throw new ArgumentException(
                        $"Expected index {Name} to have IsGlobal='{IsGlobal}', got IsGlobal='{actual.IsGlobal}' instead",
                        nameof(actual));

                if (Type != actual.Type)
                    throw new ArgumentException(
                        $"Expected index {Name} to be have Type='{Type}', got Type='{actual.Type}' instead",
                        nameof(actual));

                if (StartIndex != actual.StartIndex)
                    throw new ArgumentException(
                        $"Expected index {Name} to have StartIndex='{StartIndex}', got StartIndex='{actual.StartIndex}' instead",
                        nameof(actual));

                if (Count != actual.Count)
                    throw new ArgumentException(
                        $"Expected index {Name} to have Count='{Count}', got Count='{actual.Count}' instead",
                        nameof(actual));
            }

            public void Validate()
            {
                if (Name.HasValue == false || SliceComparer.Equals(Slices.Empty, Name))
                    throw new ArgumentException("Index name must be non-empty", nameof(Name));

                if (StartIndex < 0)
                    throw new ArgumentOutOfRangeException(nameof(StartIndex), "StartIndex cannot be negative");
            }

            public static IndexDef ReadFrom(ByteStringContext context, ref TableValueReader input)
            {
                var indexDef = new IndexDef();

                byte* currentPtr = input.Read(1, out int currentSize);
                indexDef.StartIndex = *(int*)currentPtr;

                currentPtr = input.Read(2, out currentSize);
                indexDef.Count = *(int*)currentPtr;

                currentPtr = input.Read(3, out currentSize);
                indexDef.IsGlobal = Convert.ToBoolean(*currentPtr);

                currentPtr = input.Read(4, out currentSize);
                Slice.From(context, currentPtr, currentSize, ByteStringType.Immutable, out indexDef.Name);

                return indexDef;
            }
        }

        public class DynamicKeyIndexDef : AbstractTreeIndexDef
        {
            public override TreeIndexType Type => TreeIndexType.DynamicKeyValues;

            public delegate ByteStringContext.Scope IndexEntryKeyGenerator(ByteStringContext context, ref TableValueReader value, out Slice slice);

            public delegate void OnIndexEntryChangedDelegate(Transaction tx, Slice key, int oldSize, int newSize);

            public OnIndexEntryChangedDelegate OnEntryChanged;

            public IndexEntryKeyGenerator GenerateKey;

            public ByteStringContext.Scope GetValue(ByteStringContext context, ref TableValueReader value,
                out Slice slice)
            {
                return GenerateKey(context, ref value, out slice);
            }

            public ByteStringContext.Scope GetValue(ByteStringContext context, TableValueBuilder value,
                out Slice slice)
            {
                using (context.Allocate(value.Size, out var buffer))
                {
                    // todo RavenDB-18105 : try to optimize this - avoid creating a copy of the value here

                    value.CopyTo(buffer.Ptr);
                    var reader = value.CreateReader(buffer.Ptr);
                    return GenerateKey(context, ref reader, out slice);
                }
            }

            public void OnIndexEntryChanged(Transaction tx, Slice key, int oldSize, int newSize)
            {
                OnEntryChanged?.Invoke(tx, key, oldSize, newSize);
            }

            public byte[] Serialize()
            {
                // We serialize the Type enum as ulong to be "future-proof"
                var castedType = (long)Type;

                var serializer = new TableValueBuilder
                {
                    castedType,
                    IsGlobal,
                    Name
                };

                var methodNameBytes = Encodings.Utf8.GetBytes(GenerateKey.Method.Name);
                fixed (byte* methodNamePtr = methodNameBytes)
                {
                    serializer.Add(methodNamePtr, methodNameBytes.Length);

                    Debug.Assert(GenerateKey.Method.DeclaringType?.FullName != null && GenerateKey.Method.DeclaringType.Assembly.FullName != null,
                        $"Invalid {nameof(GenerateKey)} '{GenerateKey.Method.Name}'");

                    var assemblyName = new AssemblyName(GenerateKey.Method.DeclaringType.Assembly.FullName);
                    var declaringType = $"{GenerateKey.Method.DeclaringType.FullName}, {assemblyName.Name}";
                    var declaringTypeBytes = Encodings.Utf8.GetBytes(declaringType);
                    fixed (byte* declaringTypePtr = declaringTypeBytes)
                    {
                        serializer.Add(declaringTypePtr, declaringTypeBytes.Length);

                        byte[] serialized = new byte[serializer.Size];

                        fixed (byte* destination = serialized)
                        {
                            serializer.CopyTo(destination);
                        }

                        return serialized;
                    }
                }
                //  TODO : serialize 'OnEntryChanged'
            }

            public static DynamicKeyIndexDef ReadFrom(ByteStringContext context, ref TableValueReader input)
            {
                var indexDef = new DynamicKeyIndexDef();

                byte* currentPtr = input.Read(1, out _);
                indexDef.IsGlobal = Convert.ToBoolean(*currentPtr);

                currentPtr = input.Read(2, out var currentSize);
                Slice.From(context, currentPtr, currentSize, ByteStringType.Immutable, out indexDef.Name);

                // read IndexEntryKeyGenerator method name
                currentPtr = input.Read(3, out currentSize);
                var methodName = Encodings.Utf8.GetString(currentPtr, currentSize);

                // read IndexEntryKeyGenerator declaring type
                currentPtr = input.Read(4, out currentSize);
                var declaringType = Encodings.Utf8.GetString(currentPtr, currentSize);

                //var type = System.Type.GetType(declaringType);
                var type = System.Type.GetType(declaringType);
                if (type == null)
                    throw new InvalidDataException($"Failed to get {nameof(GenerateKey)}.Method.DeclaringType from deserialized value : {declaringType}");

                var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (method == null)
                    throw new InvalidDataException($"Failed to get method-info from type : {type}, method name : {methodName}");

                if (method.IsStatic == false)
                    throw new InvalidDataException($"{nameof(GenerateKey)} must be a static method. method name : {methodName}");

                if (method.GetCustomAttribute<StorageIndexEntryKeyGeneratorAttribute>() == null)
                    throw new InvalidDataException($"{nameof(GenerateKey)} must be marked with custom attribute '{nameof(StorageIndexEntryKeyGeneratorAttribute)}'. method name : {methodName}");

                var @delegate = Delegate.CreateDelegate(typeof(IndexEntryKeyGenerator), method);
                indexDef.GenerateKey = (IndexEntryKeyGenerator)@delegate;

                //  TODO : read 'OnEntryChanged'

                return indexDef;
            }

            public void EnsureIdentical(DynamicKeyIndexDef actual)
            {
                if (actual == null)
                    throw new ArgumentNullException(nameof(actual), "Expected an index but received null");

                if (!SliceComparer.Equals(Name, actual.Name))
                    throw new ArgumentException(
                        $"Expected index to have Name='{Name}', got Name='{actual.Name}' instead",
                        nameof(actual));

                if (IsGlobal != actual.IsGlobal)
                    throw new ArgumentException(
                        $"Expected index {Name} to have IsGlobal='{IsGlobal}', got IsGlobal='{actual.IsGlobal}' instead",
                        nameof(actual));

                if (Type != actual.Type)
                    throw new ArgumentException(
                        $"Expected index {Name} to be have Type='{Type}', got Type='{actual.Type}' instead",
                        nameof(actual));

                if (GenerateKey.Method.Name != actual.GenerateKey.Method.Name)
                    throw new ArgumentException(
                        $"Expected index {Name} to have {nameof(GenerateKey)}.Method.Name='{GenerateKey.Method.Name}', " +
                        $"got {nameof(GenerateKey)}.Method.Name='{actual.GenerateKey.Method.Name}' instead",
                        nameof(actual));

                if (GenerateKey.Method.DeclaringType != actual.GenerateKey.Method.DeclaringType)
                    throw new ArgumentException(
                        $"Expected index {Name} to have {nameof(GenerateKey)}.Method.DeclaringType='{GenerateKey.Method.DeclaringType}', " +
                        $"got {nameof(GenerateKey)}.Method.DeclaringType='{actual.GenerateKey.Method.DeclaringType}' instead",
                        nameof(actual));

                //  TODO : ensure 'OnEntryChanged' identical 
            }

            public void Validate()
            {
                if (Name.HasValue == false || SliceComparer.Equals(Slices.Empty, Name))
                    throw new ArgumentException("Index name must be non-empty", nameof(Name));

                if (GenerateKey == null)
                    throw new ArgumentOutOfRangeException(nameof(GenerateKey), $"{GenerateKey} delegate cannot be null");

                if (GenerateKey.Method.DeclaringType == null)
                    throw new ArgumentOutOfRangeException(nameof(GenerateKey), $"{nameof(GenerateKey)}.Method.DeclaringType cannot be null");

                if (GenerateKey.Method.IsStatic == false)
                    throw new ArgumentOutOfRangeException(nameof(GenerateKey), $"{nameof(GenerateKey)} must be a static method");

                if (GenerateKey.Method.GetCustomAttribute<StorageIndexEntryKeyGeneratorAttribute>() == null)
                    throw new ArgumentOutOfRangeException(nameof(GenerateKey), $"{nameof(GenerateKey)} must be marked with custom attribute '{nameof(StorageIndexEntryKeyGeneratorAttribute)}'");

                //  TODO : validate 'OnEntryChanged'
            }
        }

        public class FixedSizeKeyIndexDef
        {
            public bool IsGlobal;

            public Slice Name;

            public int StartIndex = -1;

            public long GetValue(ref TableValueReader value)
            {
                var ptr = value.Read(StartIndex, out int totalSize);
                Debug.Assert(totalSize == sizeof(long), $"{totalSize} == sizeof(long) - {Name}");
                return Bits.SwapBytes(*(long*)ptr);
            }

            public long GetValue(ByteStringContext context, TableValueBuilder value)
            {
                using (value.SliceFromLocation(context, StartIndex, out Slice slice))
                {
                    return Bits.SwapBytes(*(long*)slice.Content.Ptr);
                }
            }

            public byte[] Serialize()
            {
                var serializer = new TableValueBuilder
                {
                    StartIndex,
                    IsGlobal,
                    Name
                };

                byte[] serialized = new byte[serializer.Size];

                fixed (byte* destination = serialized)
                {
                    serializer.CopyTo(destination);
                }

                return serialized;
            }

            public static FixedSizeKeyIndexDef ReadFrom(ByteStringContext context, byte* location, int size)
            {
                var input = new TableValueReader(location, size);
                var output = new FixedSizeKeyIndexDef();

                int currentSize;
                byte* currentPtr = input.Read(0, out currentSize);
                output.StartIndex = *(int*)currentPtr;

                currentPtr = input.Read(1, out currentSize);
                output.IsGlobal = Convert.ToBoolean(*currentPtr);

                currentPtr = input.Read(2, out currentSize);
                Slice.From(context, currentPtr, currentSize, ByteStringType.Immutable, out output.Name);

                return output;
            }

            public void EnsureIdentical(FixedSizeKeyIndexDef actual)
            {
                if (actual == null)
                    throw new ArgumentNullException(nameof(actual), "Expected an index but received null");

                if (!SliceComparer.Equals(Name, actual.Name))
                    throw new ArgumentException(
                        $"Expected index to have Name='{Name}', got Name='{actual.Name}' instead",
                        nameof(actual));

                if (IsGlobal != actual.IsGlobal)
                    throw new ArgumentException(
                        $"Expected index {Name} to have IsGlobal='{IsGlobal}', got IsGlobal='{actual.IsGlobal}' instead",
                        nameof(actual));

                if (StartIndex != actual.StartIndex)
                    throw new ArgumentException(
                        $"Expected index {Name} to have StartIndex='{StartIndex}', got StartIndex='{actual.StartIndex}' instead",
                        nameof(actual));

            }
        }
    }
}
