using System.Reactive.Disposables;
using Fetchling.Core;
using Microsoft.AspNetCore.Components;

namespace OfficeRkyv.UI.Components.Shared;

/// <summary>
/// A component that runs mutations of its own: each is tracked, so it reports only what this component started, its
/// failures go to the page's <see cref="FailureReporter"/>, and everything is disposed with the component.
/// </summary>
public abstract class TrackedComponent : ComponentBase, IDisposable
{
    private readonly CompositeDisposable disposables = [];

    /// <summary>The page's reporter, cascaded from its layout.</summary>
    [CascadingParameter]
    protected FailureReporter Failures { get; set; } = null!;

    /// <summary>Tracks a mutation this component created with a <c>Create…Mutation()</c> method of a facade.</summary>
    protected IMutation<TArgs, TData> Track<TArgs, TData>(IMutation<TArgs, TData> mutation, Action? onSuccess = null)
    {
        disposables.Add(mutation);
        disposables.Add(mutation.Success.Subscribe(result => _ = InvokeAsync(() => onSuccess?.Invoke())));
        disposables.Add(mutation.Failure.Subscribe(error => _ = InvokeAsync(() => Failures.Show(error))));

        return mutation;
    }

    /// <summary>Disposes something with the component.</summary>
    protected void Own(IDisposable disposable) => disposables.Add(disposable);

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
        }
    }
}
