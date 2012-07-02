using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace SignalR.Client.Net20.Infrastructure
{
    /// <summary>
    /// Represents a non-generic task that will be executed eventually.
    /// </summary>
    [ComVisible(false)]
    public class Task : Task<object>
    {
    }

    /// <summary>
    /// Represents a task that will be executed eventually.
    /// </summary>
    /// <typeparam name="T">The type of result from this task.</typeparam>
    public class Task<T>
    {
        /// <summary>
        /// The event that is called when this task is finised.
        /// </summary>
        public event EventHandler<CustomResultArgs<T>> OnFinish;

        /// <summary>
        /// Call the given function when this task is finished.
        /// </summary>
        /// <typeparam name="TFollowing">The return type of the given function.</typeparam>
        /// <param name="nextAction">The function that will be called when this task is finished.</param>
        /// <returns>A task that will return the given result type.</returns>
        public Task<TFollowing> Then<TFollowing>(Func<T,TFollowing> nextAction)
        {
            var nextEventTask = new Task<TFollowing>();
            OnFinish += (sender, e) =>
                            {
                                //Fail fast here. Need to evaluate the appropriate action in this case.
								Exception exceptionFromAction = null;
                            	TFollowing result;
								try
								{
									result = nextAction(e.ResultWrapper.Result);
								}
								catch (Exception exception)
								{
									exceptionFromAction = exception;
									result = default(TFollowing);
								}
								
                                nextEventTask.OnFinished(result, exceptionFromAction);
                            };
            return nextEventTask;
        }

        /// <summary>
        /// Call the given function when this task is finished.
        /// </summary>
        /// <param name="nextAction">The function that will be called when this task is finished.</param>
        /// <returns>A non-generic task.</returns>
        public Task Then(Action<T> nextAction)
        {
            var nextEventTask = new Task();
            OnFinish += (sender, e) =>
                            {
								Exception exceptionFromAction = null;
								try
								{
									nextAction(e.ResultWrapper.Result);
								}
								catch (Exception exception)
								{
									exceptionFromAction = exception;
								}
								nextEventTask.OnFinished(null, exceptionFromAction);
                            };
            return nextEventTask;
        }

        /// <summary>
        /// Continue with the given function when this task is finished.
        /// </summary>
        /// <param name="nextAction">The function that will be called when this task is finished.</param>
        /// <returns>A non-generic task.</returns>
        public Task ContinueWith(Action<ResultWrapper<T>> nextAction)
        {
            var nextEventTask = new Task();
        	OnFinish += (sender, e) =>
        	            	{
        	            		Exception exceptionFromAction = null;
        	            		try
        	            		{
        	            			nextAction(e.ResultWrapper);
        	            		}
        	            		catch (Exception exception)
        	            		{
        	            			exceptionFromAction = exception;
        	            		}
        	            		nextEventTask.OnFinished(null, exceptionFromAction);
        	            	};
            return nextEventTask;
        }

        /// <summary>
        /// This is the method to call when this task is finished.
        /// </summary>
        /// <param name="result">The result from the operation.</param>
        /// <param name="exception">The exception from the operation, if any occucred.</param>
        public void OnFinished(T result,Exception exception)
        {
			InnerFinish(new FinishDetail { Result = result, Exception = exception });
        }

        private void InnerFinish(FinishDetail finishDetail)
        {
            var handler = OnFinish;
            if (handler==null)
            {
				if (finishDetail.Iteration>1)
				{
					return;
				}
            	finishDetail.Iteration++;
                finishDetail.Timer = new Timer(finishCallback,finishDetail,TimeSpan.FromMilliseconds(200),TimeSpan.FromMilliseconds(-1));
                return;
            }

            handler(this,
                    new CustomResultArgs<T>
                        {
                            ResultWrapper =
                                new ResultWrapper<T> {Result = finishDetail.Result, Exception = finishDetail.Exception, IsFaulted = finishDetail.Exception != null}
                        });
        }

    	private void finishCallback(object state)
    	{
    		var finishDetail = (FinishDetail)state;
			InnerFinish(finishDetail);
			finishDetail.DisposeTimer();
    	}

    	private class FinishDetail
		{
    		public FinishDetail()
    		{
    			Iteration = 1;
    		}

			public T Result { get; set; }
			public Timer Timer { get; set; }
			public Exception Exception { get; set; }
    		public int Iteration { get; set; }

			public void DisposeTimer()
			{
				if (Timer == null) return;

				try
				{
					Timer.Dispose();
					Timer = null;
				}
				catch (ObjectDisposedException)
				{
					//Suppress!
				}
			}
		}
    }
}
