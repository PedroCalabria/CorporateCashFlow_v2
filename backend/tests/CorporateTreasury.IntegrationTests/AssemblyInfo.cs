// Each integration-test factory points EF Core and Hangfire at its throwaway PostgreSQL
// container via the process-global ConnectionStrings__Default environment variable
// (see AuthApiFactory). Running test classes in parallel would let two factories overwrite
// that single variable at once, cross-wiring them. Disable parallelization so each class
// (and its container) runs in isolation.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
