namespace ModsCommon.Extensions {
    using Colossal.UI.Binding;

    public class EnumReader<T> : IReader<T> {
        /// <inheritdoc/>
        public void Read(IJsonReader reader, out T value) {
            reader.Read(out int value2);
            value = (T)(object)value2;
        }
    }
}
