using System;
using FluentAssertions;
using Jellyfin.Plugin.CadenceConfig.Api;
using Jellyfin.Plugin.CadenceConfig.Playlists;
using Xunit;

namespace Jellyfin.Plugin.CadenceConfig.Tests
{
    public class OwnPlaylistsTests
    {
        private static readonly Guid Me = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Them = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        private static readonly Guid C = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

        [Fact]
        public void For_ReturnsOnlyPlaylistsTheUserOwns_InTheItemApiIdForm()
        {
            // GetPlaylists(user) also returns playlists merely SHARED with or open to the user.
            var ids = OwnPlaylists.For(Me, new[] { (A, Me), (B, Them), (C, Me) });

            ids.Should().Equal(A.ToString("N"), C.ToString("N"));
            ids[0].Should().NotContain("-");
        }

        [Fact]
        public void For_DropsDuplicatesAndEmptyIds()
        {
            OwnPlaylists.For(Me, new[] { (A, Me), (A, Me), (Guid.Empty, Me) })
                .Should().Equal(A.ToString("N"));
        }

        [Fact]
        public void For_AnswersNothingWithoutAUser()
        {
            // A token with no user (an API key) must not match playlists with an empty owner.
            OwnPlaylists.For(Guid.Empty, new[] { (A, Guid.Empty) }).Should().BeEmpty();
        }

        [Fact]
        public void CallerScope_ActsForTheVerifiedCaller()
        {
            CallerScope.Resolve(Me, Guid.Empty).Should().Be(Me);
            CallerScope.Resolve(Me, Me).Should().Be(Me);
        }

        [Fact]
        public void CallerScope_RefusesAnotherUsersIdAndUserlessTokens()
        {
            CallerScope.Resolve(Me, Them).Should().BeNull();
            CallerScope.Resolve(Guid.Empty, Me).Should().BeNull();
            CallerScope.Resolve(Guid.Empty, Guid.Empty).Should().BeNull();
        }
    }
}
