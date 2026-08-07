using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.CadenceConfig.Audiobooks
{
    /// <summary>
    /// Keeps the cached audiobook library fresh by dropping it whenever the Jellyfin library changes
    /// (item added / updated / removed). Registered as an <see cref="IHostedService"/> so it can attach
    /// to the library-manager events for the server's lifetime. Invalidation is cheap (just clears the
    /// cache); the next client request rebuilds it lazily — so a burst of scan events doesn't trigger a
    /// rebuild per item. Pure I/O wiring, excluded from coverage like the other service shims.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AudiobookLibraryInvalidator : IHostedService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly AudiobookLibraryService _service;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudiobookLibraryInvalidator"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager whose change events we subscribe to.</param>
        /// <param name="service">The cache to invalidate on those events.</param>
        public AudiobookLibraryInvalidator(ILibraryManager libraryManager, AudiobookLibraryService service)
        {
            _libraryManager = libraryManager;
            _service = service;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded += OnLibraryChanged;
            _libraryManager.ItemUpdated += OnLibraryChanged;
            _libraryManager.ItemRemoved += OnLibraryChanged;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _libraryManager.ItemAdded -= OnLibraryChanged;
            _libraryManager.ItemUpdated -= OnLibraryChanged;
            _libraryManager.ItemRemoved -= OnLibraryChanged;
            return Task.CompletedTask;
        }

        private void OnLibraryChanged(object? sender, ItemChangeEventArgs e)
        {
            _service.Invalidate();
        }
    }
}
