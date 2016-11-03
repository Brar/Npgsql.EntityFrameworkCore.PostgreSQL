﻿using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ArrayQueryTest : IClassFixture<ArrayFixture>
    {
        [Fact]
        public void Roundtrip()
        {
            using (var ctx = CreateContext())
            {
                var x = ctx.SomeEntities.Single(e => e.Id == 1);
                Assert.Equal(new[] { 3, 4 }, x.SomeArray);
            }
        }

        [Fact]
        public void Subscript_with_constant()
        {
            using (var ctx = CreateContext())
            {
                var actual = ctx.SomeEntities.Where(e => e.SomeArray[0] == 3).ToList();
                Assert.Equal(1, actual.Count);
                AssertContainsInSql(@"WHERE (""e"".""SomeArray""[1]) = 3");
            }
        }

        [Fact]
        public void Subscript_with_non_constant()
        {
            using (var ctx = CreateContext())
            {
                var x = 0;
                var actual = ctx.SomeEntities.Where(e => e.SomeArray[x] == 3).ToList();
                Assert.Equal(1, actual.Count);
                AssertContainsInSql(@"WHERE (""e"".""SomeArray""[@__x_0 + 1]) = 3");
            }
        }

        [Fact]
        public void Subscript_bytea_with_constant()
        {
            using (var ctx = CreateContext())
            {
                var actual = ctx.SomeEntities.Where(e => e.SomeBytea[0] == 3).ToList();
                Assert.Equal(1, actual.Count);
                AssertContainsInSql(@"WHERE (get_byte(""e"".""SomeBytea"", 0)) = 3");
            }
        }

        [Fact]
        public void SequenceEqual_with_parameter()
        {
            using (var ctx = CreateContext())
            {
                var arr = new[] { 3, 4 };
                var x = ctx.SomeEntities.Single(e => e.SomeArray.SequenceEqual(arr));
                Assert.Equal(new[] { 3, 4 }, x.SomeArray);
                AssertContainsInSql(@"WHERE ""e"".""SomeArray"" = @");
            }
        }

        [Fact]
        public void SequenceEqual_with_array_literal()
        {
            using (var ctx = CreateContext())
            {
                var x = ctx.SomeEntities.Single(e => e.SomeArray.SequenceEqual(new[] { 3, 4 }));
                Assert.Equal(new[] { 3, 4 }, x.SomeArray);
                AssertContainsInSql(@"WHERE ""e"".""SomeArray"" = ARRAY[3,4]");
            }
        }

        [Fact]
        public void Length()
        {
            using (var ctx = CreateContext())
            {
                var x = ctx.SomeEntities.Single(e => e.SomeArray.Length == 2);
                Assert.Equal(new[] { 3, 4 }, x.SomeArray);
                AssertContainsInSql(@"WHERE array_length(""e"".""SomeArray"", 1) = 2");
            }
        }

        #region Support

        ArrayFixture Fixture { get; }

        public ArrayQueryTest(ArrayFixture fixture)
        {
            Fixture = fixture;
        }

        ArrayContext CreateContext() => Fixture.CreateContext();

        void AssertContainsInSql(string expected)
            => Assert.Contains(expected, Fixture.TestSqlLoggerFactory.Sql);

        #endregion Support
    }

    public class ArrayContext : DbContext
    {
        public DbSet<SomeEntity> SomeEntities { get; set; }
        public ArrayContext(DbContextOptions options) : base(options) {}
        protected override void OnModelCreating(ModelBuilder builder)
        {

        }
    }

    public class SomeEntity
    {
        public int Id { get; set; }
        public int[] SomeArray { get; set; }
        public byte[] SomeBytea { get; set; }
        public string SomeText { get; set; }
    }

    public class ArrayFixture : IDisposable
    {
        readonly DbContextOptions _options;
        public TestSqlLoggerFactory TestSqlLoggerFactory { get; } = new TestSqlLoggerFactory();

        public ArrayFixture()
        {
            _testStore = NpgsqlTestStore.CreateScratch();
            _options = new DbContextOptionsBuilder()
                .UseNpgsql(_testStore.Connection, b => b.ApplyConfiguration())
                .UseInternalServiceProvider(
                    new ServiceCollection()
                        .AddEntityFrameworkNpgsql()
                        .AddSingleton<ILoggerFactory>(TestSqlLoggerFactory)
                        .BuildServiceProvider())
                .Options;

            using (var ctx = CreateContext())
            {
                ctx.Database.EnsureCreated();
                ctx.SomeEntities.Add(new SomeEntity
                {
                    Id=1,
                    SomeArray = new[] { 3, 4 },
                    SomeBytea = new byte[] { 3, 4 }
                });
                ctx.SomeEntities.Add(new SomeEntity
                {
                    Id=2,
                    SomeArray = new[] { 5, 6, 7 },
                    SomeBytea = new byte[] { 5, 6, 7 }
                });
                ctx.SaveChanges();
            }
        }

        readonly NpgsqlTestStore _testStore;
        public ArrayContext CreateContext() => new ArrayContext(_options);
        public void Dispose() => _testStore.Dispose();
    }
}