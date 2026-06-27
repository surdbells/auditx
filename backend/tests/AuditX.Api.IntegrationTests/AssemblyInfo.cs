// Each integration test class spins up its own SQL Server Testcontainer (IClassFixture<ApiFactory>).
// Running the classes in parallel starts one container per class simultaneously, which exhausts Docker
// host resources and causes spurious connection timeouts. Serialize the collections so at most one
// container is live at a time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
