# PusherClientDotNet

[Pusher](http://pusher.com/), in my client-side C#?!

**Note:** Presense channels haven't been implemented yet.

## Usage

Uhh, coming soon. For now, you can use the [pusher-js readme](https://github.com/pusher/pusher-js/blob/master/README.markdown),
since this is pretty much a straight port.

## Compiling

You'll need to install Microsoft's [WebSockets Prototype](http://html5labs.interoperabilitybridges.com/prototypes/websockets/websockets/info)
and install it. You may have to change the references, since in the repo they're to `C:\Program Files (x86)\...`.

As the prototype thingy is built against .NET 4.0, unfortunately this only supports, well, .NET 4.0 and up. Client WebSocket implementations
in C# are hard to come by, so if you ever find one, it'd be awesome to tell me, or fork it and swap out Microsoft's implementation! Or
maybe Microsoft can somehow be nudged to build the library against a lower version of .NET. What lovely dreams.

## Silverlight

### Microsoft Websocket Library Limitations

The Silverlight library will not work with the current version of the WebSockets prototype. This is because it requires the
`clientaccesspolicy.xml` file to be served from port 80 on the Pusher WebSockets server. Pusher are serving the file over TCP
on port 943 but in order to get Silverlight to check that port it would be necessary to decompile the WebSocket Silverlight DLL
and update the the code within WebSocketProtocol.cs from:

    this.sendArgs.SocketClientAccessPolicyProtocol = SocketClientAccessPolicyProtocol.Http;

to

    this.sendArgs.SocketClientAccessPolicyProtocol = SocketClientAccessPolicyProtocol.Tcp;

If you have any questions about this you can [drop Phil Leggetter an email](mailto:phil@leggetter.co.uk). 

### Test App Authentication

In order to trigger client events on a channel the channel must be a Private channel and that 
channel must be authenticated. In the example a HTTP handler has been used to handle the authentication 
request from the Silverlight client. This has been configured in Web.config with the following:

    <system.web>
        <httpHandlers>
            <add verb="*" path="/pusher/auth/" type="PusherSilverlightTestApp.Web.PusherAuthHandler" />
        </httpHandlers>
    </system.web>

In order for the authentication to succeed the Silverlight runtime will also make a request for 
a `clientaccesspolicy.xml` file from port 80 on the server that the test app is being hosted on. 
This means that for the demo to work when running in Visual Studio the port must be set to 80.

## How to get the PusherSilverlightTestApp.Web working

1. Ensure you have a reference to the [Pusher REST .NET libray](https://github.com/leggetter/pusher-rest-dotnet). This is also available as a [NuGet package](http://nuget.org/List/Packages/PusherRESTDotNet).
1. Get a version of the Microsoft WebSockets prototype that fetches the clientaccesspolicy.xml from port 943
2. Ensure that the YOUR_APP_KEY value in MainPage.xaml.cs has been updated to use your API key
3. Have client events turned on for your application. To do this you may need to [contact Pusher support](mailto:support@pusher.com).
4. Update the values in the Web.config file to represent your Pusher application settings

If you have any problems please [contact Pusher support](mailto:support@pusher.com)