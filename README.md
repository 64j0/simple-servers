# Simple Web APIs

This project was built so I can compare the default implementation of different Web Servers.

For this experiment, I'm interested in:

* How each server deals with not found routes;
* How each server deals with requests where the path exists, but it's associated with a different HTTP verb (i.e., request method);
* How each server expects the HTTP verb to be written.

You can find more about it reading [my blog](https://gaio.dev/).

## How to run it

You can use the `main.fsx` script to start the servers and perform the requests. The responses are written to files at the *responses/* folder.
