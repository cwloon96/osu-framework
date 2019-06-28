// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Lists;
using osu.Framework.Tests.Visual.Containers;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Tests.Visual.Drawables
{
    public class TestSceneDelayedLoadUnloadWrapper : FrameworkTestScene
    {
        private const int panel_count = 1024;

        private FillFlowContainer<Container> flow;
        private TestScrollContainer scroll;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Children = new Drawable[]
            {
                scroll = new TestScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        flow = new FillFlowContainer<Container>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        }
                    }
                }
            };
        });

        [Test]
        public void TestUnloadViaScroll()
        {
            WeakList<Container> references = new WeakList<Container>();

            AddStep("populate panels", () =>
            {
                references.Clear();

                for (int i = 0; i < 16; i++)
                    flow.Add(new Container
                    {
                        Size = new Vector2(128),
                        Children = new Drawable[]
                        {
                            new DelayedLoadUnloadWrapper(() =>
                            {
                                var container = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new TestBox { RelativeSizeAxes = Axes.Both }
                                    },
                                };

                                references.Add(container);

                                return container;
                            }, 500, 2000),
                            new SpriteText { Text = i.ToString() },
                        }
                    });

                flow.Add(
                    new Container
                    {
                        Size = new Vector2(128, 1280),
                    });
            });

            AddUntilStep("references counted", () => references.Count() == 16);

            AddAssert("check schedulers present", () => scroll.Scheduler.HasPendingTasks);

            AddStep("scroll to end", () => scroll.ScrollToEnd());

            AddUntilStep("repeating schedulers removed", () => !scroll.Scheduler.HasPendingTasks);

            AddUntilStep("references lost", () =>
            {
                GC.Collect();
                return !references.Any();
            });
        }

        [Test]
        public void TestRemovedStillUnload()
        {
            WeakList<Container> references = new WeakList<Container>();

            AddStep("populate panels", () =>
            {
                references.Clear();

                for (int i = 0; i < 16; i++)
                    flow.Add(new Container
                    {
                        Size = new Vector2(128),
                        Children = new Drawable[]
                        {
                            new DelayedLoadUnloadWrapper(() =>
                            {
                                var container = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new TestBox { RelativeSizeAxes = Axes.Both }
                                    },
                                };

                                references.Add(container);

                                return container;
                            }, 500, 2000),
                            new SpriteText { Text = i.ToString() },
                        }
                    });
            });

            AddUntilStep("references counted", () => references.Count() == 16);

            AddAssert("check schedulers present", () => scroll.Scheduler.HasPendingTasks);

            AddStep("Remove all panels", () => flow.Clear());

            AddUntilStep("repeating schedulers removed", () => !scroll.Scheduler.HasPendingTasks);

            AddUntilStep("references lost", () =>
            {
                GC.Collect();
                return !references.Any();
            });
        }

        [Test]
        public void TestManyChildrenUnload()
        {
            AddStep("populate panels", () =>
            {
                for (int i = 1; i < panel_count; i++)
                    flow.Add(new Container
                    {
                        Size = new Vector2(128),
                        Children = new Drawable[]
                        {
                            new DelayedLoadUnloadWrapper(() => new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new TestBox { RelativeSizeAxes = Axes.Both }
                                }
                            }, 500, 2000),
                            new SpriteText { Text = i.ToString() },
                        }
                    });
            });

            Func<int> childrenWithAvatarsLoaded = () =>
                flow.Children.Count(c => c.Children.OfType<DelayedLoadWrapper>().First().Content?.IsLoaded ?? false);

            int loadedCountInitial = 0;
            int loadedCountSecondary = 0;

            AddUntilStep("wait some loaded", () => (loadedCountInitial = childrenWithAvatarsLoaded()) > 5);

            AddStep("scroll down", () => scroll.ScrollToEnd());

            AddUntilStep("wait more loaded", () => (loadedCountSecondary = childrenWithAvatarsLoaded()) > loadedCountInitial);

            AddAssert("not too many loaded", () => childrenWithAvatarsLoaded() < panel_count / 4);

            AddUntilStep("wait some unloaded", () => childrenWithAvatarsLoaded() < loadedCountSecondary);
        }

        public class TestScrollContainer : BasicScrollContainer
        {
            public new Scheduler Scheduler => base.Scheduler;
        }

        public class TestBox : Container
        {
            public TestBox()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new SpriteText
                {
                    Colour = Color4.Yellow,
                    Text = @"loaded",
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }
        }
    }
}
