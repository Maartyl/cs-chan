Overview of code
================

### DbgCns.cs

Simple debug logs to console that include time and thread.

First argument is expected to be `this` so it is obvious which class logged that.

### DebugCounter.cs

Simple static utility that counts how many times has code passed through certain point.

### Unit.cs

A class that represents 'nothing' and only has 1 possible value: null. It's like void, in a sense, but can be used in generics.

### Exts.cs

Simple extension methods that only introduce syntax sugar but don't add any functionality.

### TaskCompletionCallback.cs

Like `TaskCompletionSource` but constructor requires callback that is invoked once value is delivered.

### TaskCompletionSourceEmpty.cs

`TaskCompletionSource<Unit>` with extra method `SetCompleted()`.

### ExceptionDrain.cs

Provides Task that finishes on first finished failed (or possibly cancelled) task fed into the drain.

### TaskCollector.cs

Can collect and merge exceptions from provided tasks. All tasks are merged into 1 which allows to catch exceptions without complicating the asynchronous code.

### InvokeOnceEmbeddable.cs

If something returns Task and may only be invoked once but can be called many times or it can be just 'asked for result', the first result can be stored in `TaskCompletionSource<Task>` and awaited when requested.

The idea behind this struct is to be embedded within classes that require this functionality.

### TaskHelpers.cs

#### `Bind`

Works like monadic bind on the Task monad.

#### `Flatten`

Abstraction for 2 consecutive awaits. (or identity bind)

#### `CancelledTask`

static Property with pre-made canceled Task.

### UriHelpers.cs

Provides extension method that normalizes loopback Uris so that they are equal. (for a hash map, for example)

Helpers
-------

Code not specific to project. It was written for it and is used in it but can be useful in other projects as well.
