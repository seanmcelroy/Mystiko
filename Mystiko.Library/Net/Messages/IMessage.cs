namespace Mystiko.Net.Messages
{
    using JetBrains.Annotations;

    public interface IMessage
    {
        /// <summary>
        /// Converts the record to a block chain payload
        /// </summary>
        /// <returns>A serialized string representation of the record</returns>
        [Pure, NotNull]
        byte[] ToWire();

        /// <summary>
        /// Hydrates the record from a block chain payload
        /// </summary>
        /// <param name="payload">The serialized payload of the record</param>
        void FromWire([NotNull] byte[] payload);
    }
}
