using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabiRiichi.Event;
using RabiRiichi.Event.InGame.Listener;
using RabiRiichi.Pattern;
using RabiRiichi.Riichi;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Riichi.Setup {
    public class BaseSetup {
        /// <summary> 底和 </summary>
        protected Type[] basePatterns { get; set; }

        /// <summary> 役种 </summary>
        protected Type[] stdPatterns { get; set; }

        /// <summary> 无役型役种 </summary>
        protected Type[] bonusPatterns { get; set; }

        #region Inject

        /// <summary>
        /// 初始化<see cref="basePatterns"/>，<see cref="stdPatterns"/>，<see cref="bonusPatterns"/>
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
            foreach (var pattern in basePatterns.Concat(stdPatterns).Concat(bonusPatterns)) {
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
            resolver.RegisterBonusPatterns(bonusPatterns.Select(t => (StdPattern)services.GetService(t)).ToArray());
        }

        /// <summary> 配置事件监听 </summary>
        protected virtual void RegisterEvents(EventBus eventBus) {
            BeginGameListener.Register(eventBus);
            DealHandListener.Register(eventBus);
            DrawTileListener.Register(eventBus);
            IncreaseJunListener.Register(eventBus);
            MessageSender.Register(eventBus);
            RevealDoraListener.Register(eventBus);
            WaitPlayerActionListener.Register(eventBus);
        }

        /// <summary> 初始化阶段 </summary>
        public virtual void Setup(IServiceProvider services) {
            RegisterPatterns(services);
        }
        #endregion
    }
}