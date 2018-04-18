Windows User Service
=======================
 This is a little bit of a strange service. I have some apps that need to run under a logged-in user (due 3rd party app constraints), and I wanted a way to manage them and keep their outputs in an easy-to-find place.

 Management API
 -----------
 There is a small management API included. The binding port is configurable via the config file (which will be generated on first run), and the management API url will be shown in the "Main" tab on startup.
 The management APi will allow you to list/add/remove/start/stop services.

Noteworthy things that are missing
----------
- Authentication
- Truncating output windows to not overflow memory
- Persistent Logging
- Queuing messages and having a single background worker write them all to the UI to avoid smashing the UI thread from very noisy programs.


Home
--------
https://github.com/sjasperse/windows-console-service