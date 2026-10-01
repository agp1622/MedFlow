// Each test class has its own factory that cleans up uploaded files on dispose; running classes
// in parallel could let one factory delete files another is still using.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
