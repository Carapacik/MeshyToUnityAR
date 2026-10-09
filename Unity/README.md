# StreetArtAI Unity client

Open with **Unity 6000.6.5f1**. Android ARM64, min SDK 29, target/compile SDK 37.

- **Development:** Street Art > Build Android development APK.
- **Release:** set STREETART_SIGNING_PROPERTIES to an external key.properties file, then use Street Art > Build Android signed release APK and AAB. Optionally override the keystore path with STREETART_KEYSTORE.
- **Output:** Builds/Android/.
- **Backend:** enter its HTTPS URL in the app's Server settings.

Signing properties: storeFile, storePassword, keyAlias and keyPassword. Keep signing files outside the repository.
