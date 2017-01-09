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

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using Mystiko.Database.Records;

    /// <summary>
    /// Tests the <see cref="IdentityAnnounce"/> record methods
    /// </summary>
    [TestClass]
    public class IdentityAnnounceUnitTest
    {
        /// <summary>
        /// Tests the Generate() method
        /// </summary>
        [TestMethod]
        public void Generate()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.IsNotNull(record);
            Assert.IsNotNull(record.Item1);
            Assert.IsTrue(record.Item1.DateEpoch > 0, "record.Item1.DateEpoch > 0");
            Assert.IsNotNull(record.Item1.PublicKeyX);
            Assert.AreEqual(record.Item1.PublicKeyX.Length, 32);
            Assert.IsNotNull(record.Item1.PublicKeyXBase64);
            Assert.IsNotNull(record.Item1.PublicKeyY);
            Assert.AreEqual(record.Item1.PublicKeyY.Length, 32);
            Assert.IsNotNull(record.Item1.PublicKeyYBase64);
            Assert.IsFalse(record.Item1.PublicKeyX.SequenceEqual(record.Item1.PublicKeyY));
            Assert.AreNotEqual(record.Item1.PublicKeyXBase64, record.Item1.PublicKeyYBase64);
            Assert.IsTrue(record.Item1.Nonce > 0, "record.Item1.Nonce > 0");
            Assert.IsNotNull(record.Item2);
            Assert.IsTrue(record.Item2.Length > 0, "record.Item2.Length > 0");
        }

        /// <summary>
        /// Tests the ToPayload() method
        /// </summary>
        [TestMethod]
        public void ToPayload()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.IsNotNull(record);
            Assert.IsNotNull(record.Item1);
            var payload = record.Item1.ToPayload();
            Assert.IsNotNull(payload);
            Assert.IsTrue(payload.Length > 0, "payload.Length > 0");
        }

        /// <summary>
        /// Tests the FromPayload() method
        /// </summary>
        [TestMethod]
        public void FromPayload()
        {
            var record = IdentityAnnounce.Generate(2);
            Assert.IsNotNull(record);
            Assert.IsNotNull(record.Item1);

            var rebuilt = new IdentityAnnounce();
            rebuilt.FromPayload(record.Item1.ToPayload());

            Assert.AreEqual(record.Item1.DateEpoch, rebuilt.DateEpoch);
            Assert.AreEqual(record.Item1.Nonce, rebuilt.Nonce);
            Assert.IsTrue(record.Item1.PublicKeyX.SequenceEqual(rebuilt.PublicKeyX));
            Assert.AreEqual(record.Item1.PublicKeyXBase64, rebuilt.PublicKeyXBase64);
            Assert.IsTrue(record.Item1.PublicKeyY.SequenceEqual(rebuilt.PublicKeyY));
            Assert.AreEqual(record.Item1.PublicKeyYBase64, rebuilt.PublicKeyYBase64);
            Assert.AreEqual(record.Item1.Nonce, rebuilt.Nonce);
        }

        /// <summary>
        /// Tests the FromPayload() method
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void FromPayload_Null()
        {
            var rebuilt = new IdentityAnnounce();
            // ReSharper disable once AssignNullToNotNullAttribute
            rebuilt.FromPayload(null);
        }

        /// <summary>
        /// Tests the Generate() method
        /// </summary>
        [TestMethod]
        public void Verify()
        {
            var record = IdentityAnnounce.Generate(4);
            Assert.IsNotNull(record);
            Assert.IsNotNull(record.Item1);
            Assert.IsTrue(record.Item1.Nonce > 0, "record.Item1.Nonce > 0");

            Assert.IsTrue(record.Item1.Verify(4));

            record.Item1.Nonce++;
            Assert.IsFalse(record.Item1.Verify(4));
        }
    }
}
