using Sep490_Backend.DTO;
using Sep490_Backend.Infra.Constants;
using Sep490_Backend.Infra.Enums;
using Newtonsoft.Json;
using StackExchange.Redis;
using Sep490_Backend.Services.AuthenService;

namespace Sep490_Backend.Services.CacheService
{
    public interface IPubSubService
    {
        void SubscribeInternal();
        long Publish(string channel, object data);
        void PublishSystem(object data);
    }

    public class PubSubService : IPubSubService
    {
        public ISubscriber _subscribe { get; set; }
        public ILogger<PubSubService> _logger { get; set; }
        private readonly IServiceProvider _serviceProvider;

        public PubSubService(RedisConnManager redisConnManager, ILogger<PubSubService> logger, IServiceProvider serviceProvider)
        {
            try
            {
                _logger = logger;
                _subscribe = redisConnManager.Connection.GetSubscriber();
            }
            catch
            {

            }
            _serviceProvider = serviceProvider;
        }

        public void SubscribeInternal()
        {
            _subscribe.SubscribeAsync(new RedisChannel(StaticVariable.RedisConfig.PubSubChannel, RedisChannel.PatternMode.Literal),
                async (channel, message) =>
                {
                    try
                    {
                        await HandleSubscribe(message);
                    }
                    catch
                    {

                    }
                });
        }

        public long Publish(string channel, object data)
        {
            var message = JsonConvert.SerializeObject(data);

            _subscribe.PublishAsync(channel, message);

            return 1;
        }

        public void PublishSystem(object data)
        {
            Publish(StaticVariable.RedisConfig.PubSubChannel, data);
        }

        private async Task HandleSubscribe(string message)
        {
            var msg = JsonConvert.DeserializeObject<PubSubMessage>(message);

            if (msg == null) return;

            await UpdateCache(msg);
        }

        private async Task UpdateCache(PubSubMessage msg)
        {
            //IDiscussionService _discussionService = _serviceProvider.GetService<IDiscussionService>();
            IAuthenService _authenService = _serviceProvider.GetService<IAuthenService>();
            //IHelpService _helpService = _serviceProvider.GetService<IHelpService>();
            switch (msg.PubSubEnum)
            {
                case PubSubEnum.UpdateUserMemory:
                    _authenService.UpdateUserMemory(int.Parse(msg.Data.ToString()));
                    break;
                case PubSubEnum.UpdateUserProfileMemory:
                    _authenService.UpdateUserProfileMemory(int.Parse(msg.Data.ToString()));
                    break;
                    //case PubSubEnum.UpdateHelpMemory:
                    //    _helpService.UpdateHelpMemory(int.Parse(msg.Data.ToString()));
                    //    break;
                    //case PubSubEnum.UpdateContestLeaderBoard:
                    //    _contestService.ClearLeaderBoardByKey(msg.Data.ToString());
                    //    break;
                    //case PubSubEnum.UpdateActivitySubmittedHistory:
                    //    var _userSubmitService = _serviceProvider.GetService<IUserSubmitService>();
                    //    var data = JsonConvert.DeserializeObject<CacheUserSubmittedHistoryDTO>(msg.Data.ToString());
                    //    _userSubmitService.AddActivitySubmittedHistoryStaticData(data.Key, data.UserSubmit);
                    //    break;
                    //case PubSubEnum.UpdateContestSubmittedHistory:
                    //    _contestService.ClearSubmitHistoryByKey(msg.Data.ToString());
                    //    break;
                    //case PubSubEnum.UpdateTeamInfo:
                    //case PubSubEnum.UpdateTeamMemberInfo:
                    //    var _initService = _serviceProvider.GetService<IInitService>();
                    //    await _initService.TriggerCache((int)(long)msg.Data, msg.PubSubEnum);
                    //    break;
                    //case PubSubEnum.UpdateContestsMemory:
                    //    _contestService.UpdateContestsMemory(int.Parse(msg.Data.ToString()));
                    //    break;

            }
        }
    }
}
