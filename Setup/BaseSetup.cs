using Microsoft.Extensions.DependencyInjection;
using RabiRiichi.Event;
using RabiRiichi.Event.InGame.Listener;
using RabiRiichi.Pattern;
using RabiRiichi.Resolver;
using RabiRiichi.Riichi;
using System;

namespace RabiRiichi.Setup {
    public class BaseSetup {
        /// <summary> 配置底和 </summary>
        protected virtual void RegisterBasePatterns(PatternResolver resolver) {
            // Add patterns here
        }

        /// <summary> 配置役种 </summary>
        protected virtual void RegisterStdPatterns(PatternResolver resolver) {
            // Add patterns here
        }

        /// <summary> 配置有番无役型役种 </summary>
        protected virtual void RegisterBonusPatterns(PatternResolver resolver) {
            // Add patterns here
        }

        /// <summary> 配置牌型解析 </summary>
        protected virtual void RegisterPatterns(PatternResolver resolver) {
            RegisterBasePatterns(resolver);
            RegisterStdPatterns(resolver);
            RegisterBonusPatterns(resolver);
        }

        /// <summary> 配置事件监听 </summary>
        protected virtual void RegisterEvents(EventBus eventBus) {
            BeginGameListener.Register(eventBus);
            DealHandListener.Register(eventBus);
            DrawTileListener.Register(eventBus);
            RevealDoraListener.Register(eventBus);
        }

        /// <summary> 依赖注入阶段配置服务 </summary>
        public virtual void Inject(Game game, IServiceCollection collection) {
            // 注入事件分析类
            collection.AddSingleton<ChiResolver>();
            collection.AddSingleton<KanResolver>();
            collection.AddSingleton<PonResolver>();
            collection.AddSingleton<PlayTileResolver>();
            collection.AddSingleton<RonResolver>();
            collection.AddSingleton<RiichiResolver>();
        }

        /// <summary> 初始化阶段 </summary>
        public virtual void Setup(Game game, IServiceProvider collection) {
            RegisterPatterns(game.patternResolver);
            RegisterEvents(game.eventBus);
        }
    }
}