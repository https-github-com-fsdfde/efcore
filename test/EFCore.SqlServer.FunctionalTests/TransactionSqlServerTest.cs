// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// ReSharper disable MethodHasAsyncOverload

namespace Microsoft.EntityFrameworkCore
{
    public class TransactionSqlServerTest : TransactionTestBase<TransactionSqlServerTest.TransactionSqlServerFixture>
    {
        public TransactionSqlServerTest(TransactionSqlServerFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory]
        [InlineData(true)]
        [InlineData(false)]
        public virtual async Task Savepoints_are_disabled_with_MARS(bool async)
        {
            await using var context = CreateContext();

            context.Database.SetDbConnection(
                new SqlConnection(TestStore.ConnectionString + ";MultipleActiveResultSets=True"));

            var transaction = await context.Database.BeginTransactionAsync();

            var orderId = 300;
            foreach (var _ in context.Set<TransactionCustomer>())
            {
                context.Add(new TransactionOrder { Id = orderId++, Name = "Order " + orderId });
                if (async)
                {
                    await context.SaveChangesAsync();
                }
                else
                {
                    context.SaveChanges();
                }
            }

            await transaction.CommitAsync();

            Assert.Contains(Fixture.ListLoggerFactory.Log, t => t.Id == SqlServerEventId.SavepointsDisabledBecauseOfMARS);
        }

        protected override bool SnapshotSupported
            => true;

        protected override bool AmbientTransactionsSupported
            => true;

        protected override DbContext CreateContextWithConnectionString()
        {
            var options = Fixture.AddOptions(
                    new DbContextOptionsBuilder()
                        .UseSqlServer(
                            TestStore.ConnectionString,
                            b => b.ApplyConfiguration().ExecutionStrategy(c => new SqlServerExecutionStrategy(c))))
                .UseInternalServiceProvider(Fixture.ServiceProvider);

            return new DbContext(options.Options);
        }

        public class TransactionSqlServerFixture : TransactionFixtureBase
        {
            protected override ITestStoreFactory TestStoreFactory
                => SqlServerTestStoreFactory.Instance;

            protected override void Seed(PoolableDbContext context)
            {
                base.Seed(context);

                context.Database.ExecuteSqlRaw("ALTER DATABASE [" + StoreName + "] SET ALLOW_SNAPSHOT_ISOLATION ON");
                context.Database.ExecuteSqlRaw("ALTER DATABASE [" + StoreName + "] SET READ_COMMITTED_SNAPSHOT ON");
            }

            public override void Reseed()
            {
                using var context = CreateContext();
                context.Set<TransactionCustomer>().RemoveRange(context.Set<TransactionCustomer>());
                context.Set<TransactionOrder>().RemoveRange(context.Set<TransactionOrder>());
                context.SaveChanges();

                base.Seed(context);
            }

            public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            {
                new SqlServerDbContextOptionsBuilder(
                        base.AddOptions(builder))
                    .ExecutionStrategy(c => new SqlServerExecutionStrategy(c));
                builder.ConfigureWarnings(b => b.Log(SqlServerEventId.SavepointsDisabledBecauseOfMARS));
                return builder;
            }
        }
    }
}
