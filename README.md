# faceit-webhook-leetify-demo-submit

- Receives a FaceIt webhook
- Logs in to Leetify
- Submits the FaceIt demo to Leetify

## Env variables to set:

- ApplicationSettings:FaceitUrl
- ApplicationSettings:LeetifyUrl
- ApplicationSettings:LeetifyUsername
- ApplicationSettings:LeetifyPassword

# Leetify remarks

- When having a Leetify account that was connected through OpenIdConnect with Steam, make sure to generate a new
  password
  on the Leetify settings page. This will decouple your account and lets you log in with email and password.

# FaceIt webhook

- Create an OAuth2 application in the FaceIt developer portal. In that application, create a webhook that listens for
  your user. List for the DemoReady event.