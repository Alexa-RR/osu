// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Online.Notifications.WebSocket.Events;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Menu;
using osu.Game.Screens.OnlinePlay.Lounge;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Playlists;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.PlayerSettings;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Screens.SelectV2;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Resources;
using osu.Game.Utils;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneScreenNavigation : OsuGameTestScene
    {
        private const float click_padding = 25;

        private Vector2 backButtonPosition => Game.ToScreenSpace(new Vector2(click_padding, Game.LayoutRectangle.Bottom - click_padding));

        private Vector2 optionsButtonPosition => Game.ToScreenSpace(new Vector2(click_padding, click_padding));

        [TestCase(false)]
        [TestCase(true)]
        public void TestConfirmationRequiredToDiscardPlaylist(bool withPlaylistItemAdded)
        {
            Screens.OnlinePlay.Playlists.Playlists playlistScreen = null;

            AddUntilStep("wait for dialog overlay", () => Game.ChildrenOfType<DialogOverlay>().SingleOrDefault() != null);

            PushAndConfirm(() => playlistScreen = new Screens.OnlinePlay.Playlists.Playlists());
            AddUntilStep("wait for lounge", () => (playlistScreen.CurrentSubScreen as LoungeSubScreen)?.IsLoaded == true);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddStep("open create screen", () =>
            {
                InputManager.MoveMouseTo(playlistScreen.ChildrenOfType<CreatePlaylistsRoomButton>().Single());
                InputManager.Click(MouseButton.Left);
            });

            if (withPlaylistItemAdded)
            {
                AddUntilStep("wait for settings displayed",
                    () => (playlistScreen.CurrentSubScreen as PlaylistsRoomSubScreen)?.ChildrenOfType<PlaylistsRoomSettingsOverlay>().SingleOrDefault()?.State.Value == Visibility.Visible);

                AddStep("edit playlist", () => InputManager.Key(Key.Enter));

                AddUntilStep("wait for song select", () => (playlistScreen.CurrentSubScreen as PlaylistsSongSelect)?.BeatmapSetsLoaded == true);

                AddUntilStep("wait for selection", () => !Game.Beatmap.IsDefault);

                AddStep("add item", () => InputManager.Key(Key.Enter));

                AddUntilStep("wait for return to playlist screen", () => playlistScreen.CurrentSubScreen is PlaylistsRoomSubScreen);

                AddStep("go back to song select", () =>
                {
                    InputManager.MoveMouseTo(playlistScreen.ChildrenOfType<PurpleRoundedButton>().Single(b => b.Text == "Edit playlist"));
                    InputManager.Click(MouseButton.Left);
                });

                AddUntilStep("wait for song select", () => (playlistScreen.CurrentSubScreen as PlaylistsSongSelect)?.BeatmapSetsLoaded == true);

                AddStep("press home button", () =>
                {
                    InputManager.MoveMouseTo(Game.Toolbar.ChildrenOfType<ToolbarHomeButton>().Single());
                    InputManager.Click(MouseButton.Left);
                });

                AddAssert("confirmation dialog shown", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog is not null);

                pushEscape();
                pushEscape();

                AddAssert("confirmation dialog shown", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog is not null);

                AddStep("confirm exit", () => InputManager.Key(Key.Enter));

                AddAssert("dialog dismissed", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog == null);

                exitViaEscapeAndConfirm();
            }
            else
            {
                pushEscape();
                AddAssert("confirmation dialog not shown", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog == null);

                exitViaEscapeAndConfirm();
            }
        }

        [Test]
        public void TestExitSongSelectWithEscape()
        {
            SoloSongSelect songSelect = null;
            ModSelectOverlay modSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddStep("Show mods overlay", () =>
            {
                modSelect = songSelect!.ChildrenOfType<ModSelectOverlay>().Single();
                modSelect.Show();
            });
            AddAssert("Overlay was shown", () => modSelect.State.Value == Visibility.Visible);
            pushEscape();
            AddAssert("Overlay was hidden", () => modSelect.State.Value == Visibility.Hidden);
            exitViaEscapeAndConfirm();
        }

        [Test]
        public void TestEnterGameplayWhileFilteringToNoSelection()
        {
            SoloSongSelect songSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("force selection and change filter immediately", () =>
            {
                InputManager.Key(Key.Enter);
                songSelect.ChildrenOfType<FilterControl>().Single().Search("test");
            });

            AddUntilStep("wait for player", () => !songSelect.IsCurrentScreen());
            AddStep("return to song select", () => songSelect.MakeCurrent());

            AddUntilStep("selection not lost", () => !songSelect.Beatmap.IsDefault);
            AddUntilStep("placeholder visible", () => songSelect.ChildrenOfType<NoResultsPlaceholder>().Single().State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestSongSelectBackActionHandling()
        {
            SoloSongSelect songSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());

            AddUntilStep("wait for filter control", () => filterControlTextBox().IsLoaded);

            AddStep("set filter", () => filterControlTextBox().Current.Value = "test");
            AddStep("press back", () => InputManager.Click(MouseButton.Button1));

            AddAssert("still at song select", () => Game.ScreenStack.CurrentScreen, () => Is.EqualTo(songSelect));
            AddAssert("filter cleared", () => string.IsNullOrEmpty(filterControlTextBox().Current.Value));

            AddStep("set filter again", () => filterControlTextBox().Current.Value = "test");
            AddStep("open collections dropdown", () =>
            {
                InputManager.MoveMouseTo(songSelect.ChildrenOfType<Screens.SelectV2.CollectionDropdown>().Single());
                InputManager.Click(MouseButton.Left);
            });

            AddStep("press back once", () => InputManager.Click(MouseButton.Button1));
            AddAssert("still at song select", () => Game.ScreenStack.CurrentScreen == songSelect);
            AddAssert("collections dropdown closed", () => songSelect
                                                           .ChildrenOfType<Screens.SelectV2.CollectionDropdown>().Single()
                                                           .ChildrenOfType<Dropdown<CollectionFilterMenuItem>.DropdownMenu>().Single().State == MenuState.Closed);

            AddStep("press back a second time", () => InputManager.Click(MouseButton.Button1));
            AddAssert("filter cleared", () => string.IsNullOrEmpty(filterControlTextBox().Current.Value));

            AddStep("press back a third time", () => InputManager.Click(MouseButton.Button1));
            ConfirmAtMainMenu();

            FilterControl.SongSelectSearchTextBox filterControlTextBox() => songSelect.ChildrenOfType<FilterControl.SongSelectSearchTextBox>().Single();
        }

        [Test]
        public void TestSongSelectRandomRewindButton()
        {
            Guid? originalSelection = null;
            SoloSongSelect songSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("Add two beatmaps", () =>
            {
                Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(8));
                Game.BeatmapManager.Import(TestResources.CreateTestBeatmapSetInfo(8));
            });

            AddUntilStep("wait for selected", () =>
            {
                originalSelection = Game.Beatmap.Value.BeatmapInfo.ID;
                return !Game.Beatmap.IsDefault;
            });

            AddStep("hit random", () =>
            {
                InputManager.MoveMouseTo(Game.ChildrenOfType<FooterButtonRandom>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("wait for selection changed", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.Not.EqualTo(originalSelection));

            AddStep("hit random rewind", () => InputManager.Click(MouseButton.Right));
            AddUntilStep("wait for selection reverted", () => Game.Beatmap.Value.BeatmapInfo.ID, () => Is.EqualTo(originalSelection));
        }

        [Test]
        public void TestSongSelectScrollHandling()
        {
            SoloSongSelect songSelect = null;
            double scrollPosition = 0;

            AddStep("set game volume to max", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.VolumeUniversal, 1d));
            AddUntilStep("wait for volume overlay to hide", () => Game.ChildrenOfType<VolumeOverlay>().SingleOrDefault()?.State.Value, () => Is.EqualTo(Visibility.Hidden));
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.IsLoaded);
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for beatmap", () => Game.ChildrenOfType<PanelBeatmapSet>().Any());

            // TODO: this logic can likely be removed when we fix https://github.com/ppy/osu/issues/33379
            // It should be probably be immediate in this case.
            AddWaitStep("wait for scroll", 10);

            AddStep("store scroll position", () => scrollPosition = getCarouselScrollPosition());

            AddStep("move to title wedge", () => InputManager.MoveMouseTo(
                songSelect.ChildrenOfType<BeatmapTitleWedge>().Single()));
            AddStep("scroll down", () => InputManager.ScrollVerticalBy(-1));
            AddAssert("carousel didn't move", getCarouselScrollPosition, () => Is.EqualTo(scrollPosition));

            AddRepeatStep("alt-scroll down", () =>
            {
                InputManager.PressKey(Key.AltLeft);
                InputManager.ScrollVerticalBy(-1);
                InputManager.ReleaseKey(Key.AltLeft);
            }, 5);
            AddAssert("game volume decreased", () => Game.Dependencies.Get<FrameworkConfigManager>().Get<double>(FrameworkSetting.VolumeUniversal), () => Is.LessThan(1));

            AddStep("set game volume to max", () => Game.Dependencies.Get<FrameworkConfigManager>().SetValue(FrameworkSetting.VolumeUniversal, 1d));

            AddStep("move to details area", () => InputManager.MoveMouseTo(
                songSelect.ChildrenOfType<BeatmapDetailsArea>().Single()));
            AddStep("scroll down", () => InputManager.ScrollVerticalBy(-1));
            AddAssert("carousel didn't move", getCarouselScrollPosition, () => Is.EqualTo(scrollPosition));

            AddRepeatStep("alt-scroll down", () =>
            {
                InputManager.PressKey(Key.AltLeft);
                InputManager.ScrollVerticalBy(-1);
                InputManager.ReleaseKey(Key.AltLeft);
            }, 5);
            AddAssert("game volume decreased", () => Game.Dependencies.Get<FrameworkConfigManager>().Get<double>(FrameworkSetting.VolumeUniversal), () => Is.LessThan(1));

            AddStep("move to carousel", () => InputManager.MoveMouseTo(songSelect.ChildrenOfType<BeatmapCarousel>().Single()));
            AddStep("scroll down", () => InputManager.ScrollVerticalBy(-1));
            AddAssert("carousel moved", getCarouselScrollPosition, () => Is.Not.EqualTo(scrollPosition));

            double getCarouselScrollPosition() => Game.ChildrenOfType<Carousel<BeatmapInfo>>().Single().ChildrenOfType<UserTrackingScrollContainer>().Single().Current;
        }

        /// <summary>
        /// This tests that the F1 key will open the mod select overlay, and not be handled / blocked by the music controller (which has the same default binding
        /// but should be handled *after* song select).
        /// </summary>
        [Test]
        public void TestOpenModSelectOverlayUsingAction()
        {
            SoloSongSelect songSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddStep("Show mods overlay", () => InputManager.Key(Key.F1));
            AddAssert("Overlay was shown", () => songSelect!.ChildrenOfType<ModSelectOverlay>().Single().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestAttemptPlayBeatmapWrongHashFails()
        {
            Screens.SelectV2.SongSelect songSelect = null;

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).GetResultSafely());
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("change beatmap files", () =>
            {
                FileUtils.AttemptOperation(() =>
                {
                    foreach (var file in Game.Beatmap.Value.BeatmapSetInfo.Files.Where(f => Path.GetExtension(f.Filename) == ".osu"))
                    {
                        using (var stream = Game.Storage.GetStream(Path.Combine("files", file.File.GetStoragePath()), FileAccess.ReadWrite))
                            stream.WriteByte(0);
                    }
                });
            });

            AddStep("invalidate cache", () =>
            {
                ((IWorkingBeatmapCache)Game.BeatmapManager).Invalidate(Game.Beatmap.Value.BeatmapSetInfo);
            });

            AddStep("select next difficulty", () => InputManager.Key(Key.Down));
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player loader", () => Game.ScreenStack.CurrentScreen is PlayerLoader);
            AddUntilStep("wait for song select", () => songSelect.IsCurrentScreen());
        }

        [Test]
        public void TestAttemptPlayBeatmapMissingFails()
        {
            Screens.SelectV2.SongSelect songSelect = null;

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).GetResultSafely());
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("delete beatmap files", () =>
            {
                FileUtils.AttemptOperation(() =>
                {
                    foreach (var file in Game.Beatmap.Value.BeatmapSetInfo.Files.Where(f => Path.GetExtension(f.Filename) == ".osu"))
                        Game.Storage.Delete(Path.Combine("files", file.File.GetStoragePath()));
                });
            });

            AddStep("invalidate cache", () =>
            {
                ((IWorkingBeatmapCache)Game.BeatmapManager).Invalidate(Game.Beatmap.Value.BeatmapSetInfo);
            });

            AddStep("select next difficulty", () => InputManager.Key(Key.Down));
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player loader", () => Game.ScreenStack.CurrentScreen is PlayerLoader);
            AddUntilStep("wait for song select", () => songSelect.IsCurrentScreen());
        }

        [Test]
        public void TestOffsetAdjustDuringPause()
        {
            Player player = null;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail() });
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                player = Game.ScreenStack.CurrentScreen as Player;
                return player?.IsLoaded == true;
            });

            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            checkOffset(0);

            AddStep("adjust offset via keyboard", () => InputManager.Key(Key.Minus));
            checkOffset(-1);

            AddStep("pause", () => player.ChildrenOfType<GameplayClockContainer>().First().Stop());
            AddUntilStep("wait for pause", () => player.ChildrenOfType<GameplayClockContainer>().First().IsPaused.Value, () => Is.True);
            AddStep("attempt adjust offset via keyboard", () => InputManager.Key(Key.Minus));
            checkOffset(-1);

            void checkOffset(double offset)
            {
                AddUntilStep($"control offset is {offset}", () => this.ChildrenOfType<GameplayOffsetControl>().Single().ChildrenOfType<BeatmapOffsetControl>().Single().Current.Value,
                    () => Is.EqualTo(offset));
                AddUntilStep($"database offset is {offset}", () => Game.BeatmapManager.QueryBeatmap(b => b.ID == Game.Beatmap.Value.BeatmapInfo.ID)!.UserSettings.Offset,
                    () => Is.EqualTo(offset));
            }
        }

        [Test]
        public void TestScrollSpeedAdjustDuringGameplay()
        {
            Player player = null;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("switch to mania ruleset", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Number4);
                InputManager.ReleaseKey(Key.LControl);
            });

            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail() });
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                player = Game.ScreenStack.CurrentScreen as Player;
                return player?.IsLoaded == true;
            });

            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            checkScrollSpeed(8, 8);

            AddStep("adjust scroll speed via keyboard", () => InputManager.Key(Key.F4));
            checkScrollSpeed(9, 9);

            AddStep("seek beyond 10 seconds", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(10500));
            AddUntilStep("wait for seek", () => player.ChildrenOfType<GameplayClockContainer>().First().CurrentTime, () => Is.GreaterThan(10600));
            AddStep("attempt adjust offset via keyboard", () => InputManager.Key(Key.F4));
            checkScrollSpeed(9, 9);

            AddStep("attempt adjust offset via config change", () => getConfigManager().SetValue(ManiaRulesetSetting.ScrollSpeed, 10.0));
            checkScrollSpeed(10, 9);

            void checkScrollSpeed(double configValue, double gameplayValue)
            {
                AddUntilStep($"config value is {configValue}", () => getConfigManager().Get<double>(ManiaRulesetSetting.ScrollSpeed), () => Is.EqualTo(configValue));
                AddUntilStep($"gameplay value is {gameplayValue}", () => this.ChildrenOfType<DrawableManiaRuleset>().Single().TargetTimeRange,
                    () => Is.EqualTo(DrawableManiaRuleset.ComputeScrollTime(gameplayValue)));
            }

            ManiaRulesetConfigManager getConfigManager() => ((ManiaRulesetConfigManager)Game.Dependencies.Get<IRulesetConfigCache>().GetConfigFor(new ManiaRuleset())!);
        }

        [Test]
        public void TestOffsetAdjustDuringGameplay()
        {
            Player player = null;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail() });
            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                player = Game.ScreenStack.CurrentScreen as Player;
                return player?.IsLoaded == true;
            });

            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);
            checkOffset(0);

            AddStep("adjust offset via keyboard", () => InputManager.Key(Key.Minus));
            checkOffset(-1);

            AddStep("seek beyond 10 seconds", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(10500));
            AddUntilStep("wait for seek", () => player.ChildrenOfType<GameplayClockContainer>().First().CurrentTime, () => Is.GreaterThan(10600));
            AddStep("attempt adjust offset via keyboard", () => InputManager.Key(Key.Minus));
            checkOffset(-1);

            void checkOffset(double offset)
            {
                AddUntilStep($"control offset is {offset}", () => this.ChildrenOfType<GameplayOffsetControl>().Single().ChildrenOfType<BeatmapOffsetControl>().Single().Current.Value,
                    () => Is.EqualTo(offset));
                AddUntilStep($"database offset is {offset}", () => Game.BeatmapManager.QueryBeatmap(b => b.ID == Game.Beatmap.Value.BeatmapInfo.ID)!.UserSettings.Offset,
                    () => Is.EqualTo(offset));
            }
        }

        [Test]
        public void TestRetryCountIncrements()
        {
            Player player = null;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                player = Game.ScreenStack.CurrentScreen as Player;
                return player?.IsLoaded == true;
            });

            AddAssert("retry count is 0", () => player.RestartCount == 0);

            // todo: see https://github.com/ppy/osu/issues/22220
            // tests are supposed to be immune to this edge case by the logic in TestPlayer,
            // but we're running a full game instance here, so we have to work around it manually.
            AddStep("end spectator before retry", () => Game.SpectatorClient.EndPlaying(player.GameplayState));

            AddStep("attempt to retry", () => player.ChildrenOfType<HotkeyRetryOverlay>().First().Action());
            AddAssert("old player score marked failed", () => player.Score.ScoreInfo.Rank, () => Is.EqualTo(ScoreRank.F));
            AddUntilStep("wait for old player gone", () => Game.ScreenStack.CurrentScreen != player);

            AddUntilStep("get new player", () => (player = Game.ScreenStack.CurrentScreen as Player) != null);
            AddAssert("retry count is 1", () => player.RestartCount == 1);
        }

        [Test]
        public void TestLastScoreNotNullAfterExitingPlayer()
        {
            AddUntilStep("last play null", getLastPlay, () => Is.Null);

            var getOriginalPlayer = playToCompletion();

            AddStep("attempt to retry", () => getOriginalPlayer().ChildrenOfType<HotkeyRetryOverlay>().First().Action());
            AddUntilStep("last play matches player", getLastPlay, () => Is.EqualTo(getOriginalPlayer().Score.ScoreInfo));

            AddUntilStep("wait for player", () => Game.ScreenStack.CurrentScreen != getOriginalPlayer() && Game.ScreenStack.CurrentScreen is Player);
            AddStep("exit player", () => (Game.ScreenStack.CurrentScreen as Player)?.Exit());
            AddUntilStep("last play not null", getLastPlay, () => Is.Not.Null);

            ScoreInfo getLastPlay() => Game.Dependencies.Get<SessionStatics>().Get<ScoreInfo>(Static.LastLocalUserScore);
        }

        [Test]
        public void TestRetryImmediatelyAfterCompletion()
        {
            var getOriginalPlayer = playToCompletion();

            AddStep("attempt to retry", () => getOriginalPlayer().ChildrenOfType<HotkeyRetryOverlay>().First().Action());
            AddAssert("original play isn't failed", () => getOriginalPlayer().Score.ScoreInfo.Rank, () => Is.Not.EqualTo(ScoreRank.F));
            AddUntilStep("wait for player", () => Game.ScreenStack.CurrentScreen != getOriginalPlayer() && Game.ScreenStack.CurrentScreen is Player);
        }

        [Test]
        public void TestExitImmediatelyAfterCompletion()
        {
            var player = playToCompletion();

            AddStep("attempt to exit", () => player().ChildrenOfType<HotkeyExitOverlay>().First().Action());
            AddUntilStep("wait for results", () => Game.ScreenStack.CurrentScreen is ResultsScreen);
        }

        [Test]
        public void TestShowMedalAtResults()
        {
            playToResults();

            AddStep("award medal", () => ((DummyAPIAccess)API).NotificationsClient.Receive(new SocketMessage
            {
                Event = @"new",
                Data = JObject.FromObject(new NewPrivateNotificationEvent
                {
                    Name = @"user_achievement_unlock",
                    Details = JObject.FromObject(new UserAchievementUnlock
                    {
                        Title = "Time And A Half",
                        Description = "Having a right ol' time. One and a half of them, almost.",
                        Slug = @"all-intro-doubletime"
                    })
                })
            }));
            AddUntilStep("medal overlay shown", () => Game.ChildrenOfType<MedalOverlay>().Single().State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        [Test]
        public void TestRetryFromResults()
        {
            var getOriginalPlayer = playToResults();

            AddStep("attempt to retry", () => ((ResultsScreen)Game.ScreenStack.CurrentScreen).ChildrenOfType<HotkeyRetryOverlay>().First().Action());
            AddUntilStep("wait for player", () => Game.ScreenStack.CurrentScreen != getOriginalPlayer() && Game.ScreenStack.CurrentScreen is Player);
        }

        [Test]
        public void TestDeleteAllScoresAfterPlaying()
        {
            playToResults();

            ScoreInfo score = null;
            BeatmapLeaderboardScore scorePanel = null;

            AddStep("get score", () => score = ((ResultsScreen)Game.ScreenStack.CurrentScreen).Score);

            AddAssert("ensure score is databased", () => Game.Realm.Run(r => r.Find<ScoreInfo>(score.ID)?.DeletePending == false));

            AddStep("press back button", () => Game.ChildrenOfType<BackButton>().First().Action!.Invoke());

            AddStep("show local scores",
                () => Game.ChildrenOfType<Dropdown<BeatmapLeaderboardScope>>().First().Current.Value = BeatmapLeaderboardScope.Local);

            AddUntilStep("wait for score displayed", () => (scorePanel = Game.ChildrenOfType<BeatmapLeaderboardScore>().FirstOrDefault(s => s.Score.Equals(score))) != null);

            AddStep("Clear all scores", () => Game.Dependencies.Get<ScoreManager>().Delete());

            AddUntilStep("ensure score is pending deletion", () => Game.Realm.Run(r => r.Find<ScoreInfo>(score.ID)?.DeletePending == true));

            AddUntilStep("wait for score panel removal", () => scorePanel.Parent == null);
        }

        [Test]
        public void TestDeleteScoreAfterPlaying()
        {
            playToResults();

            ScoreInfo score = null;
            BeatmapLeaderboardScore scorePanel = null;

            AddStep("get score", () => score = ((ResultsScreen)Game.ScreenStack.CurrentScreen).Score);

            AddAssert("ensure score is databased", () => Game.Realm.Run(r => r.Find<ScoreInfo>(score.ID)?.DeletePending == false));

            AddStep("press back button", () => Game.ChildrenOfType<BackButton>().First().Action!.Invoke());

            AddStep("show local scores",
                () => Game.ChildrenOfType<Dropdown<BeatmapLeaderboardScope>>().First().Current.Value = BeatmapLeaderboardScope.Local);

            AddUntilStep("wait for score displayed", () => (scorePanel = Game.ChildrenOfType<BeatmapLeaderboardScore>().FirstOrDefault(s => s.Score.Equals(score))) != null);

            AddStep("right click panel", () =>
            {
                InputManager.MoveMouseTo(scorePanel);
                InputManager.Click(MouseButton.Right);
            });

            AddStep("click delete", () =>
            {
                var dropdownItem = Game
                                   .ChildrenOfType<BeatmapLeaderboardWedge>().First()
                                   .ChildrenOfType<OsuContextMenu>().First()
                                   .ChildrenOfType<DrawableOsuMenuItem>().First(i => i.Item.Text.ToString() == "Delete");

                InputManager.MoveMouseTo(dropdownItem);
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("wait for dialog display", () => ((Drawable)Game.Dependencies.Get<IDialogOverlay>()).IsLoaded);
            AddUntilStep("wait for dialog", () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog != null);
            AddStep("confirm deletion", () => InputManager.Key(Key.Number1));
            AddUntilStep("wait for dialog dismissed", () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog == null);

            AddUntilStep("ensure score is pending deletion", () => Game.Realm.Run(r => r.Find<ScoreInfo>(score.ID)?.DeletePending == true));

            AddUntilStep("wait for score panel removal", () => scorePanel.Parent == null);
        }

        [Test]
        public void TestMenuMakesMusic()
        {
            SoloSongSelect songSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());

            AddUntilStep("wait for no track", () => Game.MusicController.CurrentTrack.IsDummyDevice);

            AddStep("return to menu", () => songSelect.Exit());

            AddUntilStep("wait for track", () => !Game.MusicController.CurrentTrack.IsDummyDevice && Game.MusicController.IsPlaying);
        }

        [Test]
        public void TestPushSongSelectAndPressBackButtonImmediately()
        {
            AddStep("push song select", () => Game.ScreenStack.Push(new SoloSongSelect()));
            AddStep("press back button", () => Game.ChildrenOfType<BackButton>().First().Action!.Invoke());

            ConfirmAtMainMenu();
        }

        [Test]
        public void TestExitSongSelectWithClick()
        {
            SoloSongSelect songSelect = null;
            ModSelectOverlay modSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddStep("Show mods overlay", () =>
            {
                modSelect = songSelect!.ChildrenOfType<ModSelectOverlay>().Single();
                modSelect.Show();
            });
            AddAssert("Overlay was shown", () => modSelect.State.Value == Visibility.Visible);

            AddStep("Move mouse to dimmed area", () => InputManager.MoveMouseTo(new Vector2(
                songSelect.ScreenSpaceDrawQuad.TopLeft.X + 1,
                songSelect.ScreenSpaceDrawQuad.TopLeft.Y + songSelect.ScreenSpaceDrawQuad.Height / 2)));
            AddStep("Click left mouse button", () => InputManager.Click(MouseButton.Left));

            AddUntilStep("Overlay was hidden", () => modSelect.State.Value == Visibility.Hidden);
            exitViaBackButtonAndConfirm();
        }

        [Test]
        public void TestModsResetOnEnteringMultiplayer()
        {
            var osuAutomationMod = new OsuModAutoplay();

            AddStep("Enable autoplay", () => { Game.SelectedMods.Value = new[] { osuAutomationMod }; });

            PushAndConfirm(() => new Screens.OnlinePlay.Multiplayer.Multiplayer());
            AddUntilStep("Mods are removed", () => Game.SelectedMods.Value.Count == 0);

            AddStep("Return to menu", () => Game.ScreenStack.CurrentScreen.Exit());
            AddUntilStep("Mods are restored", () => Game.SelectedMods.Value.Contains(osuAutomationMod));
        }

        [Test]
        public void TestExitMultiWithEscape()
        {
            PushAndConfirm(() => new Screens.OnlinePlay.Playlists.Playlists());
            exitViaEscapeAndConfirm();
        }

        [Test]
        public void TestExitMultiWithBackButton()
        {
            PushAndConfirm(() => new Screens.OnlinePlay.Playlists.Playlists());
            exitViaBackButtonAndConfirm();
        }

        [Test]
        public void TestOpenOptionsAndExitWithEscape()
        {
            AddUntilStep("Wait for options to load", () => Game.Settings.IsLoaded);
            AddStep("Enter menu", () => InputManager.Key(Key.Enter));
            AddStep("Move mouse to options overlay", () => InputManager.MoveMouseTo(optionsButtonPosition));
            AddStep("Click options overlay", () => InputManager.Click(MouseButton.Left));
            AddAssert("Options overlay was opened", () => Game.Settings.State.Value == Visibility.Visible);
            AddStep("Hide options overlay using escape", () => InputManager.Key(Key.Escape));
            AddAssert("Options overlay was closed", () => Game.Settings.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestWaitForNextTrackInMenu()
        {
            bool trackCompleted = false;

            AddUntilStep("Wait for music controller", () => Game.MusicController.IsLoaded);
            AddStep("Seek close to end", () =>
            {
                Game.MusicController.SeekTo(Game.MusicController.CurrentTrack.Length - 1000);
                Game.MusicController.CurrentTrack.Completed += () => trackCompleted = true;
            });

            AddUntilStep("Track was completed", () => trackCompleted);
            AddUntilStep("Track was restarted", () => Game.MusicController.IsPlaying);
        }

        [Test]
        public void TestModSelectInput()
        {
            AddUntilStep("Wait for toolbar to load", () => Game.Toolbar.IsLoaded);

            SoloSongSelect songSelect = null;
            ModSelectOverlay modSelect = null;

            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddStep("Show mods overlay", () =>
            {
                modSelect = songSelect!.ChildrenOfType<ModSelectOverlay>().Single();
                modSelect.Show();
            });
            AddAssert("Overlay was shown", () => modSelect.State.Value == Visibility.Visible);

            AddStep("Show mods overlay", () => modSelect.Show());

            AddStep("Change ruleset to osu!taiko", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Number2);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddAssert("Ruleset changed to osu!taiko", () => Game.Toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().Current.Value.OnlineID == 1);

            AddAssert("Mods overlay still visible", () => modSelect.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestBeatmapOptionsInput()
        {
            AddUntilStep("Wait for toolbar to load", () => Game.Toolbar.IsLoaded);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            SoloSongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("Show options overlay", () => InputManager.Key(Key.F3));
            AddUntilStep("Options overlay visible", () => this.ChildrenOfType<FooterButtonOptions.Popover>().SingleOrDefault()?.State.Value == Visibility.Visible);

            AddStep("Change ruleset to osu!taiko", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.Number2);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddAssert("Ruleset changed to osu!taiko", () => Game.Toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().Current.Value.OnlineID == 1);

            AddAssert("Options overlay still visible", () => this.ChildrenOfType<FooterButtonOptions.Popover>().Single().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestSettingsViaHotkeyFromMainMenu()
        {
            AddUntilStep("Wait for toolbar to load", () => Game.Toolbar.IsLoaded);

            AddAssert("toolbar not displayed", () => Game.Toolbar.State.Value == Visibility.Hidden);

            AddStep("press settings hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.O);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("settings displayed", () => Game.Settings.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestToolbarHiddenByUser()
        {
            AddUntilStep("Wait for toolbar to load", () => Game.Toolbar.IsLoaded);

            AddStep("Enter menu", () => InputManager.Key(Key.Enter));
            AddUntilStep("Toolbar is visible", () => Game.Toolbar.State.Value == Visibility.Visible);

            AddStep("Hide toolbar", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.T);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            pushEscape();

            AddStep("Enter menu", () => InputManager.Key(Key.Enter));

            AddAssert("Toolbar is hidden", () => Game.Toolbar.State.Value == Visibility.Hidden);

            AddStep("Enter song select", () =>
            {
                InputManager.Key(Key.Enter);
                InputManager.Key(Key.Enter);
            });

            AddAssert("Toolbar is hidden", () => Game.Toolbar.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestPushMatchSubScreenAndPressBackButtonImmediately()
        {
            TestMultiplayerComponents multiplayerComponents = null;

            PushAndConfirm(() => multiplayerComponents = new TestMultiplayerComponents());

            AddUntilStep("wait for lounge", () => multiplayerComponents.ChildrenOfType<LoungeSubScreen>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open room", () => multiplayerComponents.ChildrenOfType<LoungeSubScreen>().Single().Open());
            AddStep("press back button", () => Game.ChildrenOfType<BackButton>().First().Action!.Invoke());
            AddWaitStep("wait two frames", 2);

            AddStep("exit lounge", () => Game.ScreenStack.Exit());
            // `TestMultiplayerComponents` registers a request handler in its BDL, but never unregisters it.
            // to prevent the handler living for longer than it should be, clean up manually.
            AddStep("clean up multiplayer request handler", () => ((DummyAPIAccess)API).HandleRequest = null);
        }

        [Test]
        public void TestFeaturedArtistDisclaimerDialog()
        {
            BeatmapListingOverlay getBeatmapListingOverlay() => Game.ChildrenOfType<BeatmapListingOverlay>().FirstOrDefault();

            AddStep("Wait for notifications to load", () => Game.SearchBeatmapSet(string.Empty));
            AddUntilStep("wait for dialog overlay", () => Game.ChildrenOfType<DialogOverlay>().SingleOrDefault() != null);

            AddUntilStep("Wait for beatmap overlay to load", () => getBeatmapListingOverlay()?.State.Value == Visibility.Visible);
            AddAssert("featured artist filter is on", () => getBeatmapListingOverlay().ChildrenOfType<BeatmapSearchGeneralFilterRow>().First().Current.Contains(SearchGeneral.FeaturedArtists));
            AddStep("toggle featured artist filter",
                () => getBeatmapListingOverlay().ChildrenOfType<FilterTabItem<SearchGeneral>>().First(i => i.Value == SearchGeneral.FeaturedArtists).TriggerClick());

            AddAssert("disclaimer dialog is shown", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog != null);
            AddAssert("featured artist filter is still on", () => getBeatmapListingOverlay().ChildrenOfType<BeatmapSearchGeneralFilterRow>().First().Current.Contains(SearchGeneral.FeaturedArtists));

            AddStep("confirm", () => InputManager.Key(Key.Enter));
            AddAssert("dialog dismissed", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog == null);

            AddUntilStep("featured artist filter is off", () => !getBeatmapListingOverlay().ChildrenOfType<BeatmapSearchGeneralFilterRow>().First().Current.Contains(SearchGeneral.FeaturedArtists));
        }

        [Test]
        public void TestBeatmapListingLinkSearchOnInitialOpen()
        {
            BeatmapListingOverlay getBeatmapListingOverlay() => Game.ChildrenOfType<BeatmapListingOverlay>().FirstOrDefault();

            AddStep("open beatmap overlay with test query", () => Game.SearchBeatmapSet("test"));

            AddUntilStep("wait for beatmap overlay to load", () => getBeatmapListingOverlay()?.State.Value == Visibility.Visible);

            AddAssert("beatmap overlay sorted by relevance", () => getBeatmapListingOverlay().ChildrenOfType<BeatmapListingSortTabControl>().Single().Current.Value == SortCriteria.Relevance);
        }

        [Test]
        public void TestMainOverlaysClosesNotificationOverlay()
        {
            ChangelogOverlay getChangelogOverlay() => Game.ChildrenOfType<ChangelogOverlay>().FirstOrDefault();

            AddUntilStep("Wait for notifications to load", () => Game.Notifications.IsLoaded);
            AddStep("Show notifications", () => Game.Notifications.Show());
            AddUntilStep("wait for notifications shown", () => Game.Notifications.IsPresent && Game.Notifications.State.Value == Visibility.Visible);
            AddStep("Show changelog listing", () => Game.ShowChangelogListing());
            AddUntilStep("wait for changelog shown", () => getChangelogOverlay()?.IsPresent == true && getChangelogOverlay()?.State.Value == Visibility.Visible);
            AddAssert("Notifications is hidden", () => Game.Notifications.State.Value == Visibility.Hidden);

            AddStep("Show notifications", () => Game.Notifications.Show());
            AddUntilStep("wait for notifications shown", () => Game.Notifications.State.Value == Visibility.Visible);
            AddUntilStep("changelog still visible", () => getChangelogOverlay().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestMainOverlaysClosesSettingsOverlay()
        {
            ChangelogOverlay getChangelogOverlay() => Game.ChildrenOfType<ChangelogOverlay>().FirstOrDefault();

            AddUntilStep("Wait for settings to load", () => Game.Settings.IsLoaded);
            AddStep("Show settings", () => Game.Settings.Show());
            AddUntilStep("wait for settings shown", () => Game.Settings.IsPresent && Game.Settings.State.Value == Visibility.Visible);
            AddStep("Show changelog listing", () => Game.ShowChangelogListing());
            AddUntilStep("wait for changelog shown", () => getChangelogOverlay()?.IsPresent == true && getChangelogOverlay()?.State.Value == Visibility.Visible);
            AddAssert("Settings is hidden", () => Game.Settings.State.Value == Visibility.Hidden);

            AddStep("Show settings", () => Game.Settings.Show());
            AddUntilStep("wait for settings shown", () => Game.Settings.State.Value == Visibility.Visible);
            AddUntilStep("changelog still visible", () => getChangelogOverlay().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestOverlayClosing()
        {
            // use now playing overlay for "overlay -> background" drag case
            // since most overlays use a scroll container that absorbs on mouse down
            NowPlayingOverlay nowPlayingOverlay = null;

            AddUntilStep("Wait for now playing load", () => (nowPlayingOverlay = Game.ChildrenOfType<NowPlayingOverlay>().FirstOrDefault()) != null);

            AddStep("enter menu", () => InputManager.Key(Key.Enter));
            AddUntilStep("toolbar displayed", () => Game.Toolbar.State.Value == Visibility.Visible);

            AddStep("open now playing", () => InputManager.Key(Key.F6));
            AddUntilStep("now playing is visible", () => nowPlayingOverlay.State.Value == Visibility.Visible);

            // drag tests

            // background -> toolbar
            AddStep("move cursor to background", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomRight));
            AddStep("press left mouse button", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move cursor to toolbar", () => InputManager.MoveMouseTo(Game.Toolbar.ScreenSpaceDrawQuad.Centre));
            AddStep("release left mouse button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("now playing is hidden", () => nowPlayingOverlay.State.Value == Visibility.Hidden);

            AddStep("press now playing hotkey", () => InputManager.Key(Key.F6));

            // toolbar -> background
            AddStep("press left mouse button", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move cursor to background", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomRight));
            AddStep("release left mouse button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("now playing is still visible", () => nowPlayingOverlay.State.Value == Visibility.Visible);

            // background -> overlay
            AddStep("press left mouse button", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move cursor to now playing overlay", () => InputManager.MoveMouseTo(nowPlayingOverlay.ScreenSpaceDrawQuad.Centre));
            AddStep("release left mouse button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("now playing is still visible", () => nowPlayingOverlay.State.Value == Visibility.Visible);

            // overlay -> background
            AddStep("press left mouse button", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move cursor to background", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomRight));
            AddStep("release left mouse button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("now playing is still visible", () => nowPlayingOverlay.State.Value == Visibility.Visible);

            // background -> background
            AddStep("press left mouse button", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move cursor to left", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomLeft));
            AddStep("release left mouse button", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("now playing is hidden", () => nowPlayingOverlay.State.Value == Visibility.Hidden);

            AddStep("press now playing hotkey", () => InputManager.Key(Key.F6));

            // click tests

            // toolbar
            AddStep("move cursor to toolbar", () => InputManager.MoveMouseTo(Game.Toolbar.ScreenSpaceDrawQuad.Centre));
            AddStep("click left mouse button", () => InputManager.Click(MouseButton.Left));
            AddAssert("now playing is still visible", () => nowPlayingOverlay.State.Value == Visibility.Visible);

            // background
            AddStep("move cursor to background", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.BottomRight));
            AddStep("click left mouse button", () => InputManager.Click(MouseButton.Left));
            AddAssert("now playing is hidden", () => nowPlayingOverlay.State.Value == Visibility.Hidden);

            // move the mouse firmly inside game bounds to avoid interfering with other tests.
            AddStep("center cursor", () => InputManager.MoveMouseTo(Game.ScreenSpaceDrawQuad.Centre));
        }

        [Test]
        public void TestExitWithOperationInProgress()
        {
            int x = 0;

            AddUntilStep("wait for dialog overlay", () =>
            {
                x = 0;
                return Game.ChildrenOfType<DialogOverlay>().SingleOrDefault() != null;
            });

            AddRepeatStep("start ongoing operation", () =>
            {
                Game.Notifications.Post(new ProgressNotification
                {
                    Text = $"Something is still running #{++x}",
                    Progress = 0.5f,
                    State = ProgressNotificationState.Active,
                });
            }, 15);

            AddAssert("all notifications = 15", () => Game.Notifications.AllNotifications.Count(), () => Is.EqualTo(15));
            AddStep("Hold escape", () => InputManager.PressKey(Key.Escape));
            AddUntilStep("confirmation dialog shown", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog is ConfirmExitDialog);
            AddStep("Release escape", () => InputManager.ReleaseKey(Key.Escape));

            AddStep("cancel exit", () => InputManager.Key(Key.Escape));
            AddAssert("dialog dismissed", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog == null);

            AddStep("complete operation", () =>
            {
                this.ChildrenOfType<ProgressNotification>().ForEach(n =>
                {
                    n.Progress = 100;
                    n.State = ProgressNotificationState.Completed;
                });
            });

            AddStep("Hold escape", () => InputManager.PressKey(Key.Escape));
            AddUntilStep("Wait for intro", () => Game.ScreenStack.CurrentScreen is IntroScreen);
            AddStep("Release escape", () => InputManager.ReleaseKey(Key.Escape));

            AddUntilStep("Wait for game exit", () => Game.ScreenStack.CurrentScreen == null);
        }

        [Test]
        public void TestForceExitWithOperationInProgress()
        {
            AddStep("set hold delay to 0", () => Game.LocalConfig.SetValue(OsuSetting.UIHoldActivationDelay, 0.0));
            AddUntilStep("wait for dialog overlay", () => Game.ChildrenOfType<DialogOverlay>().SingleOrDefault() != null);

            AddRepeatStep("start ongoing operation", () =>
            {
                Game.Notifications.Post(new ProgressNotification
                {
                    Text = "Something is still running",
                    Progress = 0.5f,
                    State = ProgressNotificationState.Active,
                });
            }, 15);

            AddRepeatStep("attempt force exit", () => Game.ScreenStack.CurrentScreen.Exit(), 2);
            AddUntilStep("stopped at exit confirm", () => Game.ChildrenOfType<DialogOverlay>().Single().CurrentDialog is ConfirmExitDialog);
        }

        [Test]
        public void TestExitGameFromSongSelect()
        {
            PushAndConfirm(() => new SoloSongSelect());
            exitViaEscapeAndConfirm();

            pushEscape(); // returns to osu! logo

            AddStep("Hold escape", () => InputManager.PressKey(Key.Escape));
            AddUntilStep("Wait for intro", () => Game.ScreenStack.CurrentScreen is IntroScreen);
            AddStep("Release escape", () => InputManager.ReleaseKey(Key.Escape));
            AddUntilStep("Wait for game exit", () => Game.ScreenStack.CurrentScreen == null);
            AddStep("test dispose doesn't crash", () => Game.Dispose());
        }

        [Test]
        public void TestExitWithHoldDisabled()
        {
            AddUntilStep("wait for dialog overlay", () => Game.ChildrenOfType<DialogOverlay>().SingleOrDefault() != null);

            AddStep("set hold delay to 0", () => Game.LocalConfig.SetValue(OsuSetting.UIHoldActivationDelay, 0.0));

            AddStep("press escape twice rapidly", () =>
            {
                InputManager.Key(Key.Escape);
                Schedule(InputManager.Key, Key.Escape);
            });

            pushEscape();

            AddAssert("exit dialog is shown", () => Game.Dependencies.Get<IDialogOverlay>().CurrentDialog is ConfirmExitDialog);
        }

        [Test]
        public void TestQuickSkinEditorDoesntNukeSkin()
        {
            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddStep("open", () => InputManager.Key(Key.Space));
            AddStep("skin", () => InputManager.Key(Key.E));
            AddStep("editor", () => InputManager.Key(Key.S));
            AddStep("and close immediately", () => InputManager.Key(Key.Escape));

            AddStep("open again", () => InputManager.Key(Key.S));

            Player player = null;

            AddUntilStep("wait for player", () => (player = Game.ScreenStack.CurrentScreen as Player) != null);
            AddUntilStep("wait for gameplay still has health bar", () => player.ChildrenOfType<ArgonHealthDisplay>().Any());
        }

        [Test]
        public void TestTouchScreenDetectionAtSongSelect()
        {
            AddUntilStep("wait for settings", () => Game.Settings.IsLoaded);

            AddStep("touch logo", () =>
            {
                var button = Game.ChildrenOfType<OsuLogo>().Single();
                var touch = new Touch(TouchSource.Touch1, button.ScreenSpaceDrawQuad.Centre);
                InputManager.BeginTouch(touch);
                InputManager.EndTouch(touch);
            });
            AddAssert("touch screen detected active", () => Game.Dependencies.Get<SessionStatics>().Get<bool>(Static.TouchInputActive), () => Is.True);

            AddStep("click settings button", () =>
            {
                var button = Game.ChildrenOfType<MainMenuButton>().Last();
                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("touch screen detected inactive", () => Game.Dependencies.Get<SessionStatics>().Get<bool>(Static.TouchInputActive), () => Is.False);

            AddStep("close settings sidebar", () => InputManager.Key(Key.Escape));

            Screens.SelectV2.SongSelect songSelect = null;
            AddRepeatStep("go to solo", () => InputManager.Key(Key.P), 3);
            AddUntilStep("wait for song select", () => (songSelect = Game.ScreenStack.CurrentScreen as Screens.SelectV2.SongSelect) != null);
            AddUntilStep("wait for beatmap sets loaded", () => songSelect.CarouselItemsPresented);

            AddStep("switch to osu! ruleset", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Number1);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddStep("touch beatmap wedge", () =>
            {
                var wedge = Game.ChildrenOfType<BeatmapTitleWedge>().Single();
                var touch = new Touch(TouchSource.Touch2, wedge.ScreenSpaceDrawQuad.Centre);
                InputManager.BeginTouch(touch);
                InputManager.EndTouch(touch);
            });
            AddUntilStep("touch device mod activated", () => Game.SelectedMods.Value, () => Has.One.InstanceOf<ModTouchDevice>());

            AddStep("switch to mania ruleset", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Number4);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddUntilStep("touch device mod not activated", () => Game.SelectedMods.Value, () => Has.None.InstanceOf<ModTouchDevice>());
            AddStep("touch beatmap wedge", () =>
            {
                var wedge = Game.ChildrenOfType<BeatmapTitleWedge>().Single();
                var touch = new Touch(TouchSource.Touch2, wedge.ScreenSpaceDrawQuad.Centre);
                InputManager.BeginTouch(touch);
                InputManager.EndTouch(touch);
            });
            AddUntilStep("touch device mod not activated", () => Game.SelectedMods.Value, () => Has.None.InstanceOf<ModTouchDevice>());

            AddStep("switch to osu! ruleset", () =>
            {
                InputManager.PressKey(Key.LControl);
                InputManager.Key(Key.Number1);
                InputManager.ReleaseKey(Key.LControl);
            });
            AddUntilStep("touch device mod activated", () => Game.SelectedMods.Value, () => Has.One.InstanceOf<ModTouchDevice>());

            AddStep("click beatmap wedge", () =>
            {
                InputManager.MoveMouseTo(Game.ChildrenOfType<BeatmapTitleWedge>().Single());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("touch device mod not activated", () => Game.SelectedMods.Value, () => Has.None.InstanceOf<ModTouchDevice>());
        }

        [Test]
        public void TestTouchScreenDetectionInGame()
        {
            BeatmapSetInfo beatmapSet = null;

            PushAndConfirm(() => new SoloSongSelect());
            AddStep("import beatmap", () => beatmapSet = BeatmapImportHelper.LoadQuickOszIntoOsu(Game).GetResultSafely());
            AddUntilStep("wait for selected", () => Game.Beatmap.Value.BeatmapSetInfo.Equals(beatmapSet));
            AddStep("select", () => InputManager.Key(Key.Enter));

            Player player = null;

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return (player = Game.ScreenStack.CurrentScreen as Player) != null;
            });

            AddUntilStep("wait for track playing", () => Game.Beatmap.Value.Track.IsRunning);

            AddStep("touch", () =>
            {
                var touch = new Touch(TouchSource.Touch2, Game.ScreenSpaceDrawQuad.Centre);
                InputManager.BeginTouch(touch);
                InputManager.EndTouch(touch);
            });
            AddUntilStep("touch device mod added to score", () => player.Score.ScoreInfo.Mods, () => Has.One.InstanceOf<ModTouchDevice>());

            AddStep("exit player", () => player.Exit());
            AddUntilStep("touch device mod still active", () => Game.SelectedMods.Value, () => Has.One.InstanceOf<ModTouchDevice>());
        }

        [Test]
        public void TestExitSongSelectAndImmediatelyClickLogo()
        {
            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("press escape and then click logo immediately", () =>
            {
                InputManager.Key(Key.Escape);
                clickLogoWhenNotCurrent();
            });

            void clickLogoWhenNotCurrent()
            {
                if (songSelect.IsCurrentScreen())
                    Scheduler.AddOnce(clickLogoWhenNotCurrent);
                else
                {
                    InputManager.MoveMouseTo(Game.ChildrenOfType<OsuLogo>().Single());
                    InputManager.Click(MouseButton.Left);
                }
            }
        }

        [Test]
        public void TestPresentBeatmapAfterDeletion()
        {
            BeatmapSetInfo beatmap = null;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());
            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("delete selected beatmap", () =>
            {
                beatmap = Game.Beatmap.Value.BeatmapSetInfo;
                Game.BeatmapManager.Delete(Game.Beatmap.Value.BeatmapSetInfo);
            });

            AddUntilStep("nothing selected", () => Game.Beatmap.IsDefault);
            AddStep("present deleted beatmap", () => Game.PresentBeatmap(beatmap));
            AddAssert("still nothing selected", () => Game.Beatmap.IsDefault);
        }

        private Func<Player> playToResults()
        {
            var player = playToCompletion();
            AddUntilStep("wait for results", () => (Game.ScreenStack.CurrentScreen as ResultsScreen)?.IsLoaded == true);
            return player;
        }

        private Func<Player> playToCompletion()
        {
            Player player = null;

            IWorkingBeatmap beatmap() => Game.Beatmap.Value;

            Screens.SelectV2.SongSelect songSelect = null;
            PushAndConfirm(() => songSelect = new SoloSongSelect());
            AddUntilStep("wait for song select", () => songSelect.CarouselItemsPresented);

            AddStep("import beatmap", () => BeatmapImportHelper.LoadQuickOszIntoOsu(Game).WaitSafely());

            AddUntilStep("wait for selected", () => !Game.Beatmap.IsDefault);

            AddStep("set mods", () => Game.SelectedMods.Value = new Mod[] { new OsuModNoFail(), new OsuModDoubleTime { SpeedChange = { Value = 2 } } });

            AddStep("press enter", () => InputManager.Key(Key.Enter));

            AddUntilStep("wait for player", () =>
            {
                DismissAnyNotifications();
                return (player = Game.ScreenStack.CurrentScreen as Player) != null;
            });

            AddUntilStep("wait for track playing", () => beatmap().Track.IsRunning);
            AddStep("seek to near end", () => player.ChildrenOfType<GameplayClockContainer>().First().Seek(beatmap().Beatmap.HitObjects[^1].StartTime - 1000));
            AddUntilStep("wait for complete", () => player.GameplayState.HasPassed);

            return () => player;
        }

        private void pushEscape() =>
            AddStep("Press escape", () => InputManager.Key(Key.Escape));

        private void exitViaEscapeAndConfirm()
        {
            pushEscape();
            ConfirmAtMainMenu();
        }

        private void exitViaBackButtonAndConfirm()
        {
            AddStep("Move mouse to backButton", () => InputManager.MoveMouseTo(backButtonPosition));
            AddStep("Click back button", () => InputManager.Click(MouseButton.Left));
            ConfirmAtMainMenu();
        }
    }
}
