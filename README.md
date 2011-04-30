PusherClientDotNet
==================

[Pusher](http://pusher.com/), in my client-side C#?!

**Note:** Presense channels haven't been implemented yet.

Usage
-----

Uhh, coming soon. For now, you can use the [pusher-js readme](https://github.com/pusher/pusher-js/blob/master/README.markdown),
since this is pretty much a straight port.

Compiling
---------

You'll need to install Microsoft's [WebSockets Prototype](http://html5labs.interoperabilitybridges.com/prototypes/websockets/websockets/info)
and install it. You may have to change the references, since in the repo they're to `C:\Program Files (x86)\...`.

As the prototype thingy is built against .NET 4.0, unfortunately this only supports, well, .NET 4.0 and up. Client WebSocket implementations
in C# are hard to come by, so if you ever find one, it'd be awesome to tell me, or fork it and swap out Microsoft's implementation! Or
maybe Microsoft can somehow be nudged to build the library against a lower version of .NET. What lovely dreams.