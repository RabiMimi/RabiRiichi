using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Events;
using RabiRiichi.Events.InGame.Listener;
using RabiRiichi.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Core.Setup {
    public class BaseSetup {
        /// <summary> 底和 </summary>
        protected readonly List<Type> basePatterns = new();

        /// <summary> 役种 </summary>
        protected readonly List<Type> stdPatterns = new();

        #region Inject

        /// <summary> 注册Base Pattern </summary>
        protected void AddBasePattern<T>() where T : BasePattern
            => basePatterns.Add(typeof(T));

        /// <summary> 注册Std Pattern </summary>
        protected void AddStdPattern<T>() where T : StdPattern
            => stdPatterns.Add(typeof(T));

        /// <summary> 移除Base Pattern </summary>
        protected bool RemoveBasePattern<T>() where T : BasePattern
            => basePatterns.Remove(typeof(T));

        /// <summary> 移除Std Pattern </summary>
        protected bool RemoveStdPattern<T>() where T : StdPattern
            => stdPatterns.Remove(typeof(T));

        /// <summary>
        /// 初始化<see cref="basePatterns"/>，<see cref="stdPatterns"/>
        /// </summary>
        protected virtual void InitPatterns() {
            // Init patterns here
        }

        private void InjectPatternsHelper(
            IServiceCollection collection,
            Type pattern,
            HashSet<Type> injectedPatterns) {
            if (injectedPatterns.Contains(pattern))
                return;
            collection.TryAddSingleton(pattern);
            injectedPatterns.Add(pattern);
            // Try add dependencies
            var ctors = pattern.GetConstructors();
            if (ctors.Length > 0) {
                var ctor = ctors[0];
                var ctorParams = ctor.GetParameters();
                foreach (var param in ctorParams) {
                    InjectPatternsHelper(collection, param.ParameterType, injectedPatterns);
                }
            }
        }

        private void InjectPatterns(IServiceCollection collection) {
            var injectedPatterns = new HashSet<Type>();
            foreach (var pattern in basePatterns.Concat(stdPatterns)) {
                InjectPatternsHelper(collection, pattern, injectedPatterns);
            }
        }

        /// <summary> 注入事件分析类 </summary>
        protected virtual void InjectResolvers(IServiceCollection collection) {
            // Inject resolvers here
        }

        /// <summary> 依赖注入阶段配置服务 </summary>
        public virtual void Inject(Game game, IServiceCollection collection) {
            // 注入事件分析类
            InjectResolvers(collection);

            // 注入牌型
            InitPatterns();
            InjectPatterns(collection);
        }

        #endregion

        #region Register

        /// <summary> 配置牌型解析 </summary>
        private void RegisterPatterns(IServiceProvider services) {
            var resolver = services.GetService<PatternResolver>();
            resolver.RegisterBasePatterns(basePatterns.Select(t => (BasePattern)services.GetService(t)).ToArray());
            resolver.RegisterStdPatterns(stdPatterns.Select(t => (StdPattern)services.GetService(t)).ToArray());
        }

        /// <summary> 配置事件监听 </summary>
        protected virtual void RegisterEvents(EventBus eventBus) {
            // InGame
            AddKanListener.Register(eventBus);
            AddTileListener.Register(eventBus);
            AgariListener.Register(eventBus);
            ApplyScoreListener.Register(eventBus);
            BeginGameListener.Register(eventBus);
            CalcScoreListener.Register(eventBus);
            ClaimTileListener.Register(eventBus);
            ConcludeGameListener.Register(eventBus);
            DealerFirstTurnListener.Register(eventBus);
            DealHandListener.Register(eventBus);
            DiscardTileListener.Register(eventBus);
            DrawTileListener.Register(eventBus);
            IncreaseJunListener.Register(eventBus);
            KanListener.Register(eventBus);
            LateClaimTileListener.Register(eventBus);
            NextGameListener.Register(eventBus);
            NextPlayerListener.Register(eventBus);
            RevealDoraListener.Register(eventBus);
            RyuukyokuListener.Register(eventBus);
            SetFuritenListener.Register(eventBus);
            SetIppatsuListener.Register(eventBus);
            SetMenzenListener.Register(eventBus);
            SetRiichiListener.Register(eventBus);
            StopGameListener.Register(eventBus);
            WaitPlayerActionListener.Register(eventBus);

            // Essential
            InitGameEvent.Register(eventBus);
            EventBroadcast.Register(eventBus);
            SyncGameStateListener.Register(eventBus);
        }

        /// <summary> 初始化阶段 </summary>
        public virtual void Setup(IServiceProvider services) {
            RegisterPatterns(services);
            RegisterEvents(services.GetService<EventBus>());
        }
        #endregion
    }
}