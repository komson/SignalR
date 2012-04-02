using System.Configuration;
using System.Web;
using System.Web.Routing;
using SignalR.Hubs;

namespace SignalR.Hosting.AspNet.Routing
{
    public class HubDispatcherRouteHandler : IRouteHandler
    {
        private string _url;
        private readonly IDependencyResolver _resolver;

        public HubDispatcherRouteHandler(string url, IDependencyResolver resolver)
        {
            _url = VirtualPathUtility.ToAbsolute(url);

        	overrideUrlWithPublished();
            _resolver = resolver;
        }

    	private void overrideUrlWithPublished()
    	{
			var publishedUrl = ConfigurationManager.AppSettings["PublishedUrl"];
			if (!string.IsNullOrEmpty(publishedUrl))
			{
				_url = publishedUrl;
			}
    	}

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var dispatcher = new HubDispatcher(_url);
            return new AspNetHandler(_resolver, dispatcher);
        }
    }
}
