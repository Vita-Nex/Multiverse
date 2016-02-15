# Multiverse
Server/Client Portal SDK

If you've ever wanted to try your hand at developing a Server/Client application from scratch, the Multiverse SDK can help!

This software was originally written for the Ultima-Shards project and is the foundation for the Gateway that allows us to list multiple shards in the server-list and have them all selectable.

This software can be referenced by any .NET 4.0+ application and exposes features to allow you to maintain and develop a server and client using an in-house packet protocol designed around the patterns used by RunUO.

You can begin using this software in your RunUO/ServUO shard by editing Data/Assemblies.cfg and adding Multiverse.dll to the list.
Make sure to compile and include your Multiverse.dll in your shard's root directory.

If you use Visual Studio, you will need to add a new reference to Multiverse.dll in your "Scripts" project.

Sample executables for both Server and Client contexts have been provided with the source code.

When linking multiple shards together, you'll have to decide which shard should be the Server and which should be the Client and configure the Portal.ServerID and Portal.ClientID accordingly.
The Portal.Server EndPoint should be set to an external IP address in Client context and an internal IP address in Server context.

You can literally communicate any data between the Server/Client, from a single byte to a complex object serialized to a byte array. The possibilities are endless.

The Server/Client uses a basic authorization system whereby the literal Portal.AuthKey is concatenated with a rounding of the current time and hashed with SHA1. The Client will always send a Handshake Packet as soon as a connection to a valid server is made. If the server matches the incoming key to it's own key (both use the same key generation algorithm) then the authentication will be successful and the client will remain connected. If the authentication fails, the client will be disposed immediately. Because the auth key is salted with a time, it is only valid for a short while.

The Portal, when running in a Server context, can handle multiple Clients. The PortalClient class is used in both contexts to describe the current connection. In a Server context, the PortalClient represents the remote client. In a Client context, it represents the local client.
The context of a client can be checked with PortalClient.IsLocal (client context) or PortalClient.IsRemote (server context). Both properties are always opposite values; if one is true, the other is surely false.

This software can be used to develop your own game server/client from scratch, all the ugly work is already done, so you can begin writing new PortalPackets and registering new PortalPacketHandlers in no time!

As always - Enjoy! :)
