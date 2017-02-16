// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IdentityAnnounceUnitTest.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   Defines the IdentityAnnounceUnitTest type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko.Library.Tests.Database.Records
{
    using System;
    using System.Linq;

    using Mystiko.Database.Records;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="IdentityAnnounce"/> record methods
    /// </summary>
    public class IdentityAnnounceUnitTest
    {
        /// <summary>
        /// Tests the Generate() method
        /// </summary>
        [Fact]
        public void Generate()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.NotNull(record);
            Assert.NotNull(record.Item1);
            Assert.True(record.Item1.DateEpoch > 0, "record.Item1.DateEpoch > 0");
            Assert.NotNull(record.Item1.PublicKeyX);
            Assert.Equal(record.Item1.PublicKeyX.Length, 32);
            Assert.NotNull(record.Item1.PublicKeyXBase64);
            Assert.NotNull(record.Item1.PublicKeyY);
            Assert.Equal(record.Item1.PublicKeyY.Length, 32);
            Assert.NotNull(record.Item1.PublicKeyYBase64);
            Assert.False(record.Item1.PublicKeyX.SequenceEqual(record.Item1.PublicKeyY));
            Assert.NotEqual(record.Item1.PublicKeyXBase64, record.Item1.PublicKeyYBase64);
            Assert.True(record.Item1.Nonce > 0, "record.Item1.Nonce > 0");
            Assert.NotNull(record.Item2);
            Assert.True(record.Item2.Length > 0, "record.Item2.Length > 0");
        }

        /// <summary>
        /// Tests the ToPayload() method
        /// </summary>
        [Fact]
        public void ToPayload()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.NotNull(record);
            Assert.NotNull(record.Item1);
            var payload = record.Item1.ToPayload();
            Assert.NotNull(payload);
            Assert.True(payload.Length > 0, "payload.Length > 0");
        }

        /// <summary>
        /// Tests the FromPayload() method
        /// </summary>
        [Fact]
        public void FromPayload()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.NotNull(record);
            Assert.NotNull(record.Item1);

            var rebuilt = new IdentityAnnounce();
            rebuilt.FromPayload(record.Item1.ToPayload());

            Assert.Equal(record.Item1.DateEpoch, rebuilt.DateEpoch);
            Assert.Equal(record.Item1.Nonce, rebuilt.Nonce);
            Assert.True(record.Item1.PublicKeyX.SequenceEqual(rebuilt.PublicKeyX));
            Assert.Equal(record.Item1.PublicKeyXBase64, rebuilt.PublicKeyXBase64);
            Assert.True(record.Item1.PublicKeyY.SequenceEqual(rebuilt.PublicKeyY));
            Assert.Equal(record.Item1.PublicKeyYBase64, rebuilt.PublicKeyYBase64);
            Assert.Equal(record.Item1.Nonce, rebuilt.Nonce);
        }

        /// <summary>
        /// Tests the FromPayload() method
        /// </summary>
        [Fact]
        public void FromPayload_Null()
        {
            var rebuilt = new IdentityAnnounce();
            // ReSharper disable once AssignNullToNotNullAttribute
            Assert.Throws<ArgumentNullException>(() => rebuilt.FromPayload(null));
        }

        /// <summary>
        /// Tests the Generate() method
        /// </summary>
        [Fact]
        public void Verify()
        {
            var record = IdentityAnnounce.Generate(4);
            Assert.NotNull(record);
            Assert.NotNull(record.Item1);
            Assert.True(record.Item1.Nonce > 0, "record.Item1.Nonce > 0");

            Assert.True(record.Item1.Verify(4));

            record.Item1.Nonce++;
            Assert.False(record.Item1.Verify(4));
        }
    }
}
