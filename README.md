# Arctis Battery Monitor 
Display your Arctis headset battery level in the task bar. No UI, no shenanigans, just a background task checking your headset vitals.  
Don't bother opening SteelSeries GG to check on your battery level ever again ! ABM works on his own as a totally independant application.  
> [!NOTE]
> If your headset is not listed, create a PR or specifically ask for it.
  
> [!WARNING]
> Only 2.4Ghz connectivity is supported.  
> All listed headsets are *theoritically* supported.
  
| List of supported headsets | Support |
| -------------------------- | ------- |
| Arctis Pro Wireless | Not tested |
| Arctis 7 (2017) | Not tested
| Arctis 7 (2019) | Not tested |
| Arctis Pro (2019) | Not tested |
| Arctis Pro GameDac | Not tested |
| Arctis 9 | Not tested |
| Arctis 1 Wireless | Not tested |
| Arctis 1 Xbox | Not tested |
| Arctis 7X | Not tested |
| Arctis 7+ | Verified |
| Arctis 7P+ | Not tested |
| Arctis 7X+ | Not tested |
| Arctis 7 Destiny Plus | Not tested |
| Arctis Nova 7 | Not tested |
| Arctis Nova 7X | Not tested |
| Arctis Nova 7X v2 | Not tested |
| Arctis Nova 7P | Not tested |
| Arctis Nova 7 Diablo IV | Not tested |
| Arctis Nova 5 | Not tested |
| Arctis Nova 5X | Not tested |

Once installed, ABM starts on computer start and will display in the system notification tray :  
<img width="217" height="182" alt="image" src="https://github.com/user-attachments/assets/ddf217cf-438c-4053-a2ec-104b1bc77168" />  
If you only have one headset you probably won't need to do anything. As long as no headset is detected or successfully connnected, ABM will retry every 15 seconds.  


## 0.1.0 is here !
  
The first release of ABM finally lands.  
This version provides the following features :  
- Automatically starts on Windows start
- Switch between multiple devices
- EN and FR localization
- Automatically check for updates

The battery level is as accurate as it can get, SteelSeries decided to only return five battery steps.
