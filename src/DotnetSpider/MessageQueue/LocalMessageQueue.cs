using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotnetSpider.MessageQueue
{
	/// <summary>
	/// 简化版的本地消息队列
	/// 1. 发布会把消息推送到所有订阅了对应 topic 的消费者
	/// 2. 只能对 topic 做取消订阅，会导致所有订阅都取消。
	/// </summary>
	public class LocalMessageQueue : IMessageQueue
	{
		private readonly ConcurrentDictionary<string, Action<string>> _consumers =
			new ConcurrentDictionary<string, Action<string>>();

		private readonly ILogger _logger;

		/// <summary>
		/// 构造方法
		/// </summary>
		/// <param name="logger">日志接口</param>
		public LocalMessageQueue(ILogger<LocalMessageQueue> logger)
		{
			_logger = logger;
		}

		/// <summary>
		/// 推送消息到指定 topic
		/// </summary>
		/// <param name="topic">topic</param>
		/// <param name="messages">消息</param>
		/// <returns></returns>
		public Task PublishAsync(string topic, params string[] messages)
		{
			Publish(topic, messages);
			return Task.CompletedTask;
		}

		public void Publish(string topic, params string[] messages)
		{
			if (messages == null || messages.Length == 0)
			{
#if DEBUG
				var stackTrace = new StackTrace();
				_logger.LogDebug($"推送空消息到 Topic {topic}: {stackTrace}");
#endif
				return;
			}

			if (_consumers.TryGetValue(topic, out Action<string> consumer))
			{
				foreach (var message in messages)
				{
					Task.Factory.StartNew(() =>
					{
						try
						{
							consumer(message);
						}
						catch (Exception e)
						{
							_logger.LogError($"Topic {topic} 消费消息 {message} 失败: {e}");
						}
					}).ConfigureAwait(false).GetAwaiter();
				}
			}
			else
			{
#if DEBUG
				var stackTrace = new StackTrace();
				_logger.LogDebug($"Topic {topic} 未被订阅: {stackTrace}");
#endif
			}
		}

		/// <summary>
		/// 订阅 topic
		/// </summary>
		/// <param name="topic">topic</param>
		/// <param name="action">消息消费的方法</param>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void Subscribe(string topic, Action<string> action)
		{
			_consumers.AddOrUpdate(topic, x => action, (t, a) => action);
		}

		/// <summary>
		/// 取消订阅 topic
		/// </summary>
		/// <param name="topic">topic</param>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void Unsubscribe(string topic)
		{
			_consumers.TryRemove(topic, out _);
		}
	}
}