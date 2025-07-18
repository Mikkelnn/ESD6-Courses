﻿using BFSAlgo.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests.Unit
{
    public class BitmapTests
    {
        [Fact]
        public void Bitmap_ShouldInitializeWithCorrectSize()
        {
            int size = 1000;
            var bitmap = new Bitmap(size);

            Assert.Equal(size, bitmap.MaxNodeCount);
            Assert.True(bitmap.ByteSize == 16 * sizeof(ulong)); // 16 as 1000 / 64 == 15.625 i.e we need atleast 16 ulong
        }

        [Fact]
        public void SetAndGet_ShouldWorkCorrectly()
        {
            var bitmap = new Bitmap(128);

            bitmap.Set(5);
            bitmap.Set(127);

            Assert.True(bitmap.Get(5));
            Assert.True(bitmap.Get(127));
            Assert.False(bitmap.Get(0));
            Assert.False(bitmap.Get(126));
        }

        [Fact]
        public void Get_ShouldReturnFalseForUnsetBits()
        {
            var bitmap = new Bitmap(128);

            for (uint i = 0; i < 128; i++)
            {
                Assert.False(bitmap.Get(i));
            }
        }

        [Fact]
        public void Set_ShouldThrow_WhenIndexOutOfRange()
        {
            var bitmap = new Bitmap(128);

            Assert.Throws<IndexOutOfRangeException>(() => bitmap.Set(128)); // valid range is 0..127
            Assert.Throws<IndexOutOfRangeException>(() => bitmap.Set(1000));
        }

        [Fact]
        public void Get_ShouldThrow_WhenIndexOutOfRange()
        {
            var bitmap = new Bitmap(128);

            Assert.Throws<IndexOutOfRangeException>(() => bitmap.Get(128));  // valid range is 0..127
            Assert.Throws<IndexOutOfRangeException>(() => bitmap.Get(999));
        }

        [Fact]
        public void SetIfNot_ShouldReturnSetForNewBitsCorrrect()
        {
            // Setup
            var bitmap = new Bitmap(64);
            
            // Act
            bitmap.SetIfNot(32);

            // Assert
            Assert.True(bitmap.Get(32));
        }

        [Fact]
        public void SetIfNot_ShouldReturnTrueForNewBitsSet()
        {
            // Setup
            var bitmap = new Bitmap(64);

            // Act
            bool wasSet = bitmap.SetIfNot(32);

            // Assert
            Assert.True(wasSet);
        }

        [Fact]
        public void SetIfNot_ShouldReturnFalseForAlreadySetBits()
        {
            // Setup
            var bitmap = new Bitmap(64);
            bitmap.Set(32);

            // Act
            bool wasSet = bitmap.SetIfNot(32);

            // Assert
            Assert.False(wasSet);
        }

        [Fact]
        public void SetIfNot_ShouldNotChangeIfNotSet()
        {
            // Setup
            var bitmap = new Bitmap(64);
            bitmap.Set(32);

            var expected = new Bitmap(64);
            expected.Set(32);

            // Act
            bitmap.SetIfNot(32);

            // Assert
            Assert.Equal(expected.AsReadOnlyMemory, bitmap.AsReadOnlyMemory);
        }

        [Fact]
        public void SetIfNOt_ShouldThrow_WhenIndexOutOfRange()
        {
            var bitmap = new Bitmap(128);

            Assert.Throws<IndexOutOfRangeException>(() => bitmap.SetIfNot(128));  // valid range is 0..127
            Assert.Throws<IndexOutOfRangeException>(() => bitmap.SetIfNot(999));
        }

        [Fact]
        public void Or_ShouldCombineTwoBitmapsCorrectly()
        {
            var bitmap1 = new Bitmap(128);
            var bitmap2 = new Bitmap(128);

            bitmap1.Set(5);
            bitmap2.Set(10);

            bitmap1.Or(bitmap2);

            Assert.True(bitmap1.Get(5));
            Assert.True(bitmap1.Get(10));
        }

        [Fact]
        public void Or_ShouldThrowIfSizesMismatch()
        {
            var bitmap1 = new Bitmap(64);
            var bitmap2 = new Bitmap(128);

            Assert.Throws<InvalidOperationException>(() => bitmap1.Or(bitmap2));
        }

        [Fact]
        public void AsReadOnlyMemory_ShouldReturnCorrectBytes()
        {
            var bitmap = new Bitmap(128);
            bitmap.Set(1);
            bitmap.Set(65);

            var memory = bitmap.AsReadOnlyMemory;
            Assert.Equal(bitmap.ByteSize, memory.Length);

            // Check that expected bits are set
            byte[] bytes = memory.ToArray();
            int firstByte = bytes[0];
            int secondLongFirstByte = bytes[8];

            Assert.Equal(0b_0000_0010, firstByte); // Bit 1
            Assert.Equal(0b_0000_0010, secondLongFirstByte); // Bit 1 of second ulong (64 + 1)
        }

        [Fact]
        public void OverwriteFromByteArray_ShouldOverwriteCorrectly()
        {
            var bitmap = new Bitmap(128);

            var newBitmap = new Bitmap(128);
            newBitmap.Set(10);
            newBitmap.Set(90);

            var bytes = newBitmap.AsReadOnlyMemory.ToArray();
            bitmap.OverwriteFromByteArray(bytes);

            Assert.True(bitmap.Get(10));
            Assert.True(bitmap.Get(90));
            Assert.False(bitmap.Get(5));
        }

        [Fact]
        public void OverwriteFromByteArray_ShouldThrowOnWrongSize()
        {
            var bitmap = new Bitmap(128);
            byte[] wrongSizeArray = new byte[bitmap.ByteSize / 2];

            Assert.Throws<ArgumentException>(() => bitmap.OverwriteFromByteArray(wrongSizeArray));
        }

        [Fact]
        public void IsAllSet_ShouldReturnTrueWhenAllBitsAreSet()
        {
            // test with size that result in internal not filled
            // this is done as 129 is the first bit in the 3. ulong internally
            // we should be able to handle this
            var bitmap = new Bitmap(129);

            for (uint i = 0; i < bitmap.MaxNodeCount; i++)
            {
                bitmap.Set(i);
            }

            Assert.True(bitmap.IsAllSet());
        }

        [Fact]
        public void IsAllSet_ShouldReturnFalseWhenNotAllBitsAreSet()
        {
            var bitmap = new Bitmap(128);

            for (uint i = 0; i < bitmap.MaxNodeCount - 1; i++)
            {
                bitmap.Set(i);
            }

            Assert.False(bitmap.IsAllSet());
        }

        [Fact]
        public void IsAllSet_ShouldReturnFalseWhenNotAllBitsAreSetWhenInternalMotFilled()
        {
            // test with size that result in internal not filled
            // this is done as 129 is the first bit in the 3. ulong internally
            // we should be able to handle this
            var bitmap = new Bitmap(129);

            for (uint i = 0; i < bitmap.MaxNodeCount - 1; i++)
            {
                bitmap.Set(i);
            }

            Assert.False(bitmap.IsAllSet());
        }

        [Fact]
        public void CountSetBits_AllZeros_ReturnsZero()
        {
            var bitmap = new Bitmap(64); // One ulong (64 bits)
            Assert.Equal(0u, bitmap.CountSetBits());
        }

        [Fact]
        public void CountSetBits_AllOnes_Returns64()
        {
            var bitmap = new Bitmap(64);
            for (uint i = 0; i < 64; i++)
                bitmap.Set(i);

            Assert.Equal(64u, bitmap.CountSetBits());
        }

        [Fact]
        public void CountSetBits_SingleBit_ReturnsOne()
        {
            var bitmap = new Bitmap(64);
            bitmap.Set(3);
            Assert.Equal(1u, bitmap.CountSetBits());
        }

        [Fact]
        public void CountSetBits_AlternateBits_ReturnsCorrect()
        {
            var bitmap = new Bitmap(8);
            bitmap.Set(1);
            bitmap.Set(3);
            bitmap.Set(5);
            Assert.Equal(3u, bitmap.CountSetBits());
        }

        [Fact]
        public void CountSetBits_TwoUlongs_MixedBits_ReturnsCorrect()
        {
            var bitmap = new Bitmap(128); // 2 ulongs
            bitmap.Set(0);
            bitmap.Set(64);
            bitmap.Set(127);
            Assert.Equal(3u, bitmap.CountSetBits());
        }

        [Fact]
        public void CountSetBits_UnalignedSize_ReturnsCorrect()
        {
            var bitmap = new Bitmap(70);
            for (uint i = 0; i < 70; i += 2)
                bitmap.Set(i);

            Assert.Equal(35u, bitmap.CountSetBits());
        }
    }
}
