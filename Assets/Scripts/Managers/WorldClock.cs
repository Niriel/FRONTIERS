using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Timers;
using Frontiers.Data;
using Frontiers.World;

namespace Frontiers
{
		public class WorldClock : Manager
		{
				public static WorldClock Get;
				//event manager
				public TimeActionReceiver TimeActions;

				public double TimeScale {
						get {
								return  mARTimeScale;
						}
				}

				public static double TimescaleTarget {
						get {
								return  mARTimeScaleTarget;
						}
				}

				#region cycle update coroutine functions

				public override void WakeUp()
				{
						Get = this;
						mParentUnderManager = false;
						mARTimeScaleTarget	= 1.0f;
						//set this so we can zero out our RT
						StopWatch.Start();
						mRTime = 0f;
						mRTLastUpdateTime = 0f;
				}

				public override void OnGameLoadStart()
				{
						//set the current adjusted real time to the last saved realtime offset
						mARTime = Profile.Get.CurrentGame.GameTimeOffset;
				}

				public override void OnGameStart()
				{
						SetTargetSpeed(1.0f);
				}

				public void Update()
				{
						//-----REAL TIME-----//
						//mRTime is the amount of real time that has passed since WakeUp was called
						//using datetime because unity's realtimesincestartup sucks
						mRTime = StopWatch.Elapsed.TotalSeconds;
						//mRTDeltaTime this is the amount of real time that has passed since the last frame
						//this includes time that has passed while we've been paused
						mRTDeltaTime = mRTime - mRTLastUpdateTime;
						mRTDeltaTimeSmooth = Lerp(mRTDeltaTimeSmooth, mRTDeltaTime, 0.5);
						mRTLastUpdateTime = mRTime;

						//-----ADJUSTED REAL TIME-----//
						//this is where we adjust real time to our game needs
						//the adjusted real time is:
						//- the real time passed since the game SESSION has started (not since the program loaded)
						//- minus any time passed while the game is paused
						//- multiplied by a time scale set by the player
						if (GameManager.Is(FGameState.InGame)) {
								mARTDeltaTime = mRTDeltaTime * mARTimeScale + mLastARTDeltaTimeAdded;
						} else {
								mARTDeltaTime = 0.0;
						}
						mARTimeScale = mARTimeScaleTarget;//just turn it off and on for now Lerp (mARTimeScale, mARTimeScaleTarget, mTimeScaleChangeSpeed);
						if (mARTimeScale > gMaxTimeScale) {
								mARTimeScale = gMaxTimeScale;
						}
						if (mARTimeScale < 0) {
								mARTimeScale = 0;
						}
						mARTime = mARTime + mARTDeltaTime;//add the delta time without the offset
						//we can stop if we're any of these things
						//the game won't be set etc
						if (GameManager.Is(
								    FGameState.WaitingForGame |
								    FGameState.Unloading |
								    FGameState.Startup |
								    FGameState.Quitting |
								    FGameState.Saving)) {
								UnityEngine.Time.timeScale = 1f;
								return;
						}

						mSkippingAhead = mARTimeScale > 1f || mLastARTDeltaTimeAdded != 0.0;

						//reset last delta time added
						mLastARTDeltaTimeAdded = 0.0;

						//set our cycles - the values are in RT but we use the ART plus the offset
						mARTOffsetTime = mARTime + Profile.Get.CurrentGame.WorldTimeOffset;
						gHourCycleCurrentART = (mARTOffsetTime % gHourCycleRT);
						gDayCycleCurrentART = (mARTOffsetTime % gDayCycleRT);
						gMonthCycleCurrentART = (mARTOffsetTime % gMonthCycleRT);
						gSeasonCycleCurrentRT = (mARTOffsetTime % gSeasonCycleRT);
						gYearCycleCurrentART = (mARTOffsetTime % gYearCycleRT);
						gCenturyCycleCurrentART = (mARTOffsetTime % gCenturyCycleRT);

						//-----WORLD TIME TIME-----//
						//this is what in-game entities use to calculate time
						//this is also where we apply game time offsets for seasons and day/night cycles etc
						//mWCTime is the world time from the start of the GAME (not the game session)
						//it's a straightforward conversion from mARTime plus an offset from the player's profile
						mWCTime = RTSecondsToGameSeconds(mARTOffsetTime);// Profile.Get.CurrentGame.GameTimeOffset;
						mWCDeltaTime = mWCTime - mWCLastUpdatedTime;
						mWCLastUpdatedTime = mWCTime;
						gTimeOfDayCurrent = HourOfDayToTimeOfDay(HourOfDay);

						//broadcast events
						if (!SuspendMessagesThisFrame) {
								if (HourOfDay != mLastHour) {
										TimeActions.ReceiveAction(TimeActionType.HourStart, Time);
								}
								mLastHour = HourOfDay;
								if (IsDay != mIsDay) {
										if (IsDay) {
												TimeActions.ReceiveAction(TimeActionType.DaytimeStart, Time);
										} else {
												TimeActions.ReceiveAction(TimeActionType.NightTimeStart, Time);
										}
								}
								mIsDay = IsDay;
						}
						SuspendMessagesThisFrame = false;

						//-----TIMESCALE-----//
						//the unity timescale affects things like physics and animation
						//setting this can mess up a lot of stuff but leaving it alone produces strange effects
						//like everything looking normal while we fast travel
						//we can't set it to zero when paused because the fps controller doesn't like it
						//so we end up with a matrix-style super-slo-mo effect
						//whatever, imperfect solution
						if (GameManager.Is(FGameState.InGame)) {
								UnityEngine.Time.timeScale = (float)mARTimeScale;
						} else if (GameManager.Is(FGameState.GamePaused)) {
								UnityEngine.Time.timeScale = 0f;
						} else {
								UnityEngine.Time.timeScale = 1.0f;
						}

						//-----DAYS/MONTHS/YEARS-----//
						gDaysSinceBeginningOfTime = (int)System.Math.Floor(mWCTime / gDayCycleWT);
						gMonthsSinceBeginningOfTime = (int)System.Math.Floor(mWCTime / gMonthCycleWT);
						gYearsSinceBeginningOfTime = (int)System.Math.Floor(mWCTime / gYearCycleWT);

						//-----SEAONS-----//
						switch (MonthOfYear) {
								case 11:
								case 0:
								case 1:
										gSeasonCurrent = TimeOfYear.SeasonWinter;
										break;

								case 2:
								case 3:
								case 4:
										gSeasonCurrent = TimeOfYear.SeasonSpring;
										break;

								case 5:
								case 6:
								case 7:
										gSeasonCurrent = TimeOfYear.SeasonSummer;
										break;

								case 8:
								case 9:
								case 10:
										gSeasonCurrent = TimeOfYear.SeasonAutumn;
										break;
						}

						#if UNITY_EDITOR
						TimeScaleEditor = mARTimeScale;
						#endif

						//reference
						//		aa_TimeMidnight				= 1,		//12am
						//		ab_TimePostMidnight			= 2,		// 2am
						//		ac_TimePreDawn				= 4,		// 4am
						//		ad_TimeDawn					= 8,		// 6am
						//		ae_TimePostDawn				= 16,		// 8am
						//		af_TimePreNoon				= 32,		//10am
						//		ag_TimeNoon					= 64,		//12pm
						//		ah_TimePostNoon				= 128,		// 2pm
						//		ai_TimePreDusk				= 256,		// 4pm
						//		aj_TimeDusk					= 512,		// 6pm
						//		ak_TimePostDusk				= 1024,		// 8pm
						//		al_TimePreMidnight			= 2048,		//10pm
				}

				public void SetTargetSpeed(double timeScaleTarget)
				{
						mARTimeScaleTarget = System.Math.Min(timeScaleTarget, gMaxTimeScale);
				}

				#endregion

				#region cycle properties

				//TODO prune these most aren't necessary any more
				//returns true of timeOfDay corresponds to the actual time of day
				public static bool Is(TimeOfDay timeOfDay)
				{
						return Flags.Check((uint)gTimeOfDayCurrent, (uint)timeOfDay, Flags.CheckType.MatchAny);
				}
				//return true if timeOfYear corresponds to the actual time of year
				public static bool Is(TimeOfYear timeOfYear)
				{
						return Flags.Check((uint)gTimeOfYearCurrent, (uint)timeOfYear, Flags.CheckType.MatchAny);
				}

				public static double WorldTimeSinceWorldCreated {
						get {
								return 0.0f;
						}
				}

				public static double RealTime {
						get {
								return  mRTime;
						}
				}

				public static double AdjustedRealTime {
						get {
								return  mARTOffsetTime;//this includes the offset from the profile / game
						}
				}

				public static double Time {
						get {
								return  mWCTime;
						}
				}

				public static double HourCycleCurrent {
						get {
								return  gHourCycleCurrentART;
						}
				}

				public static double DayCycleCurrent {
						get {
								return  gDayCycleCurrentART;
						}
				}

				public static double MoonCycleCurrentNormalized {
						get {
								return (gMonthCycleCurrentART / gMonthCycleRT);
						}
				}

				public static double DayCycleCurrentNormalized {
						get {
								return (gDayCycleCurrentART / gDayCycleRT);
						}
				}

				public static double HalfDayCycleCurrentNormalized {
						get {
								return  ((gDayCycleCurrentART % (gDayCycleCurrentART / 2)) / (gDayCycleRT * 0.5));
						}
				}

				public static double DayCycleNormalizedDistanceFromNoon {
						get {
								return Mathf.Abs((float)((DayCycleCurrentNormalized - 0.5f) * 2.0));
						}
				}

				public static double DayCycleNormalizedDistanceFromMidnight {
						get {
								return 1.0f - DayCycleNormalizedDistanceFromNoon;
						}
				}

				public static double MonthCycleCurrent {
						get {
								return  gMonthCycleCurrentART;
						}
				}

				public static double SeasonCycleCurrent {
						get {
								return  gSeasonCycleCurrentRT;
						}
				}

				public static double SeasonCycleNormalized {
						get {
								return (gSeasonCycleCurrentRT / gSeasonCycleRT);
						}
				}

				public static double SeasonCycleNormalizedDistanceFromSummer {
						get {
								return Mathf.Abs((float)((DayCycleCurrentNormalized - 0.5f) * 2.0f));
						}
				}

				public static double SeasonCycleNormalizedDistanceFromWinter {
						get {
								return 1.0f - SeasonCycleNormalizedDistanceFromSummer;
						}
				}

				public static double YearCycleCurrent {
						get {
								return  gYearCycleCurrentART;
						}
				}

				public static double CenturyCycleCurrent {
						get {
								return  gCenturyCycleCurrentART;
						}
				}

				public static TimeOfDay TimeOfDayCurrent {
						get {
								return gTimeOfDayCurrent;
						}
				}

				public static TimeOfYear TimeOfYearCurrent {
						get {
								return gTimeOfYearCurrent;
						}
				}

				public static TimeOfYear SeasonCurrent {
						get {
								return gSeasonCurrent;
						}
				}

				public static int DaysSinceBeginningOfTime {
						get {
								return gDaysSinceBeginningOfTime;
						}
				}

				public static int MonthsSinceBeginningOfTime {
						get {
								return gMonthsSinceBeginningOfTime;
						}
				}

				public static int YearsSinceBeginningOfTime {
						get {
								return gYearsSinceBeginningOfTime;
						}
				}

				public static bool IsTimeOfDay(BehaviorTOD timeOfDay)
				{
						switch (timeOfDay) {
								case BehaviorTOD.All:
								default:
										return true;

								case BehaviorTOD.Diurnal:
										return IsDay;

								case BehaviorTOD.Nocturnal:
										return IsNight;

								case BehaviorTOD.None:
										return false;
						}
				}

				public static bool IsTimeOfDay(TimeOfDay timeOfDay)
				{
						return Flags.Check((uint)timeOfDay, (uint)gTimeOfDayCurrent, Flags.CheckType.MatchAll);
				}

				public static bool IsTimeOfYear(TimeOfYear timeOfYear)
				{
						return Flags.Check((uint)SeasonCurrent, (uint)timeOfYear, Flags.CheckType.MatchAny);
				}

				public static bool IsNight {
						get {
								return mLastHour > Globals.DayHourEnd || mLastHour < Globals.DayHourStart;
						}
				}

				public static bool IsDay {
						get {
								return !IsNight;//mLastHour > Globals.DayHourStart || mLastHour < Globals.DayHourEnd;
						}
				}

				#endregion

				#region clock properties

				public int SecondOfDay {
						get {
								return (int)System.Math.Floor(gDayCycleCurrentART * gSecondsPerRTSecond) + 1;
						}
				}

				public int MinuteOfDay {
						get {
								return (int)System.Math.Floor(gDayCycleCurrentART * gMinutesPerRTSecond) + 1;
						}
				}

				public int MinuteOfHour {
						get {
								return ((int)System.Math.Floor(gDayCycleCurrentART * gMinutesPerRTSecond) % 60) + 1;
						}
				}

				public int HourOfDay {
						get {
								return (int)System.Math.Floor(gDayCycleCurrentART / gHourCycleRT) + 1;
						}
				}

				public int HourOfDay12HourClock {
						get {
								return ((int)System.Math.Floor(gDayCycleCurrentART / gHourCycleRT) % 12) + 1;
						}
				}

				public int DayOfMonth {
						get {
								return (int)System.Math.Floor(gMonthCycleCurrentART / gDayCycleRT) + 1;
						}
				}

				public int DayOfSeason {
						get {
								return (int)System.Math.Floor(gSeasonCycleCurrentRT / gDayCycleRT) + 1;
						}
				}

				public int DayOfYear {
						get {
								return (int)System.Math.Floor(gYearCycleCurrentART / gDayCycleRT) + 1;
						}
				}

				public int MonthOfSeason {
						get {
								return (int)System.Math.Floor(gSeasonCycleCurrentRT / gMonthCycleRT) + 1;
						}
				}

				public int MonthOfYear {
						get {
								return (int)System.Math.Floor(gYearCycleCurrentART / gMonthCycleRT) + 1;
						}
				}

				public int SeasonOfYear {
						get {
								return (int)System.Math.Floor(gYearCycleCurrentART / gSeasonCycleRT);
						}
				}

				public int YearOfCentury {
						get {
								return (int)System.Math.Floor(gCenturyCycleCurrentART / gYearCycleRT);
						}
				}

				public int HoursTilDawn {
						get {
								int dawn = 6;
								if (HourOfDay <= dawn) {
										return dawn - HourOfDay;
								} else {
										return (24 - HourOfDay) + dawn;
								}
						}
				}

				#endregion

				#region helper functions / classes

				//this is used in place of WaitForSeconds ( ) in coroutines because it works when timescale is different
				public static IEnumerator WaitForRTSeconds(double time)
				{
						double start = RealTime;
						while (RealTime < start + time) {
								yield return null;
						}
				}

				public TimeOfDay TimeOfDayAfter(TimeOfDay timeOfDay)
				{	//ugly, fix this
						TimeOfDay nextTimeOfDay = TimeOfDay.aa_TimeMidnight;
						switch (timeOfDay) {
								case TimeOfDay.aa_TimeMidnight:
										nextTimeOfDay = TimeOfDay.ab_TimePostMidnight;
										break;
								case TimeOfDay.ab_TimePostMidnight:
										nextTimeOfDay = TimeOfDay.ac_TimePreDawn;
										break;
								case TimeOfDay.ac_TimePreDawn:
										nextTimeOfDay = TimeOfDay.ad_TimeDawn;
										break;
								case TimeOfDay.ad_TimeDawn:
										nextTimeOfDay = TimeOfDay.ae_TimePostDawn;
										break;
								case TimeOfDay.ae_TimePostDawn:
										nextTimeOfDay = TimeOfDay.af_TimePreNoon;
										break;
								case TimeOfDay.af_TimePreNoon:
										nextTimeOfDay = TimeOfDay.ag_TimeNoon;
										break;
								case TimeOfDay.ag_TimeNoon:
										nextTimeOfDay = TimeOfDay.ah_TimePostNoon;
										break;
								case TimeOfDay.ah_TimePostNoon:
										nextTimeOfDay = TimeOfDay.ai_TimePreDusk;
										break;
								case TimeOfDay.ai_TimePreDusk:
										nextTimeOfDay = TimeOfDay.aj_TimeDusk;
										break;
								case TimeOfDay.aj_TimeDusk:
										nextTimeOfDay = TimeOfDay.ak_TimePostDusk;
										break;
								case TimeOfDay.ak_TimePostDusk:
										nextTimeOfDay = TimeOfDay.al_TimePreMidnight;
										break;
								case TimeOfDay.al_TimePreMidnight:
										nextTimeOfDay = TimeOfDay.aa_TimeMidnight;
										break;
								default:
										break;
						}

						return nextTimeOfDay;
				}

				public TimeOfDay TimeOfDayBefore(TimeOfDay timeOfDay)
				{	//ugly, fix this
						TimeOfDay prevTimeOfDay = TimeOfDay.aa_TimeMidnight;
						switch (timeOfDay) {
								case TimeOfDay.ab_TimePostMidnight:
										prevTimeOfDay = TimeOfDay.aa_TimeMidnight;
										break;
								case TimeOfDay.ac_TimePreDawn:
										prevTimeOfDay = TimeOfDay.ab_TimePostMidnight;
										break;
								case TimeOfDay.ad_TimeDawn:
										prevTimeOfDay = TimeOfDay.ac_TimePreDawn;
										break;
								case TimeOfDay.ae_TimePostDawn:
										prevTimeOfDay = TimeOfDay.ad_TimeDawn;
										break;
								case TimeOfDay.af_TimePreNoon:
										prevTimeOfDay = TimeOfDay.ae_TimePostDawn;
										break;
								case TimeOfDay.ag_TimeNoon:
										prevTimeOfDay = TimeOfDay.af_TimePreNoon;
										break;
								case TimeOfDay.ah_TimePostNoon:
										prevTimeOfDay = TimeOfDay.ag_TimeNoon;
										break;
								case TimeOfDay.ai_TimePreDusk:
										prevTimeOfDay = TimeOfDay.ah_TimePostNoon;
										break;
								case TimeOfDay.aj_TimeDusk:
										prevTimeOfDay = TimeOfDay.aa_TimeMidnight;
										break;
								case TimeOfDay.ak_TimePostDusk:
										prevTimeOfDay = TimeOfDay.aj_TimeDusk;
										break;
								case TimeOfDay.al_TimePreMidnight:
										prevTimeOfDay = TimeOfDay.ak_TimePostDusk;
										break;
								case TimeOfDay.aa_TimeMidnight:
										prevTimeOfDay = TimeOfDay.al_TimePreMidnight;
										break;
								default:
										break;
						}
			
						return prevTimeOfDay;
				}

				public int TimeOfDayToHourOfDay(TimeOfDay timeOfDay)
				{	//ugly, fix this
						int hourOfDay = 0;
						switch (timeOfDay) {
								case TimeOfDay.ab_TimePostMidnight://	= 2,		// 2am
										hourOfDay = 2;
										break;
								case TimeOfDay.ac_TimePreDawn://		= 4,		// 4am
										hourOfDay = 4;
										break;
								case TimeOfDay.ad_TimeDawn://			= 8,		// 6am
										hourOfDay = 6;
										break;
								case TimeOfDay.ae_TimePostDawn://		= 16,		// 8am
										hourOfDay = 8;
										break;
								case TimeOfDay.af_TimePreNoon://		= 32,		//10am
										hourOfDay = 10;
										break;
								case TimeOfDay.ag_TimeNoon://			= 64,		//12pm
										hourOfDay = 12;
										break;
								case TimeOfDay.ah_TimePostNoon://		= 128,		// 2pm
										hourOfDay = 14;
										break;
								case TimeOfDay.ai_TimePreDusk://		= 256,		// 4pm
										hourOfDay = 16;
										break;
								case TimeOfDay.aj_TimeDusk://			= 512,		// 6pm
										hourOfDay = 18;
										break;
								case TimeOfDay.ak_TimePostDusk://		= 1024,		// 8pm
										hourOfDay = 20;
										break;
								case TimeOfDay.al_TimePreMidnight://	= 2048,		//10pm
										hourOfDay = 22;
										break;
								case TimeOfDay.aa_TimeMidnight://		= 1,		//12am
										hourOfDay = 24;
										break;
								default:
										break;
						}

						return hourOfDay;
				}

				public TimeOfDay HourOfDayToTimeOfDay(int hourOfDay)
				{	//ugly, fix this
						TimeOfDay timeOfDay = TimeOfDay.aa_TimeMidnight;
						switch (hourOfDay) {
								case 1:
								case 2:
										timeOfDay = TimeOfDay.aa_TimeMidnight;
										break;
				
								case 3:
								case 4:
										timeOfDay = TimeOfDay.ab_TimePostMidnight;
										break;
				
								case 5:
								case 6:
										timeOfDay = TimeOfDay.ac_TimePreDawn;
										break;
				
								case 7:
								case 8:
										timeOfDay = TimeOfDay.ad_TimeDawn;
										break;
				
								case 9:
								case 10:
										timeOfDay = TimeOfDay.ae_TimePostDawn;
										break;
				
								case 11:
								case 12:
										timeOfDay = TimeOfDay.af_TimePreNoon;
										break;
				
								case 13:
								case 14:
										timeOfDay = TimeOfDay.ag_TimeNoon;
										break;
				
								case 15:
								case 16:
										timeOfDay = TimeOfDay.ah_TimePostNoon;
										break;
				
								case 17:
								case 18:
										timeOfDay = TimeOfDay.ai_TimePreDusk;
										break;
				
								case 19:
								case 20:
										timeOfDay = TimeOfDay.aj_TimeDusk;
										break;
				
								case 21:
								case 22:
										timeOfDay = TimeOfDay.ak_TimePostDusk;
										break;			
				
								case 23:
								case 24:
										timeOfDay = TimeOfDay.al_TimePreMidnight;
										break;			
				
								default:
										break;
						}
						return timeOfDay;
				}

				public static double YearsToSeconds(double years)
				{
						return years * 11352960000;
				}

				public static double MonthsToSeconds(double months)
				{
						return months * 2592000;
				}

				public static double DaysToSeconds(double days)
				{
						return days * 86400;
				}

				public static double HoursToSeconds(double hours)
				{
						return hours * 3600;
				}

				public static double HoursToGameHours(double hours)
				{
						return RTSecondsToGameSeconds(HoursToSeconds(hours));
				}

				public static double GameSecondsToRTSeconds(double gameSeconds)
				{
						return  (gameSeconds / gSecondsPerRTSecond);
				}

				public static double RTSecondsToGameSeconds(double RTSeconds)
				{
						return  (RTSeconds * gSecondsPerRTSecond);
				}

				public static double RTSecondsToGameHours(double RTSeconds)
				{
						return  ((RTSeconds * gSecondsPerRTSecond) / gHourCycleSeconds);
				}

				public static double RTSecondsToGameMinutes(double RTSeconds)
				{
						return  ((RTSeconds * gSecondsPerRTSecond) / gMinuteCycleSeconds);
				}

				public static double GameHoursToRTSeconds(double gameHours)
				{
						return  (gameHours / gHoursPerRTSecond);
				}

				public static double GameSecondsToGameMinutes(double gameSeconds)
				{
						return  (gameSeconds / gHourCycleMinutes);
				}

				public static double GameSecondsToGameHours(double gameSeconds)
				{
						return  (gameSeconds / gHourCycleSeconds);
				}

				public static double GameSecondsToGameDays(double gameSeconds)
				{
						return  (gameSeconds / gDayCycleSeconds);
				}

				public static string TimeOfYearToString(TimeOfYear selectedSeasonality)
				{
						switch (selectedSeasonality) {
								case TimeOfYear.SeasonSummer:
								default:
										return "Summer";

								case TimeOfYear.SeasonAutumn:
										return "Autumn";

								case TimeOfYear.SeasonWinter:
										return "Winter";

								case TimeOfYear.SeasonSpring:
										return "Spring";
						}
				}
				//replacement for Mathf.Lerp which uses floats
				public static double Lerp(double from, double to, double amount)
				{
						return ((to - from) * amount + from);
				}

				public static double FutureTime(int numberOf, TimeUnit unit)
				{
						double futureTime = Time;
						switch (unit) {
								case TimeUnit.Hour:
										futureTime += RTSecondsToGameSeconds(gRTSecondsPerGameHour * numberOf);
										break;

								case TimeUnit.Day:
										futureTime += RTSecondsToGameSeconds(gRTSecondsPerGameHour * numberOf);
										break;

								default:
										break;
						}
						return futureTime;
				}

				public static void ResetAbsoluteTime()
				{
						mARTime = 0f;
						mARTDeltaTime = 0f;
						mARLastUPdateTime = 0f;
						mARTOffsetTime = 0f;
				}

				public static void AddARTDeltaTime(double deltaTime)
				{
						mLastARTDeltaTimeAdded += deltaTime;
				}

				public static double RTDeltaTime {
						get {
								return  mRTDeltaTime;
						}
				}

				public static double RTDeltaTimeSmooth {
						get {
								return  mRTDeltaTimeSmooth;
						}
				}

				public static double ARTDeltaTime {
						get {
								return mARTDeltaTime;
						}
				}

				public static double DeltaTime {
						get {
								return  mWCDeltaTime;
						}
				}

				public static double DeltaTimeHours {
						get {
								return GameSecondsToGameHours(mWCDeltaTime);
						}
				}

				public static double DeltaTimeMinutes {
						get {
								return GameSecondsToGameMinutes(mWCDeltaTime);
						}
				}

				public int HoursUntilTimeOfDay(TimeOfDay targetTime)
				{
						int hoursUntilTimeOfDay = 0;
						if (IsTimeOfDay(targetTime)) {
								return hoursUntilTimeOfDay;
						}

						int currentHourOfDay = HourOfDay;
						int targetHourOfDay = TimeOfDayToHourOfDay(targetTime);

						if (currentHourOfDay < targetHourOfDay) {
								hoursUntilTimeOfDay = targetHourOfDay - currentHourOfDay;
						} else {
								hoursUntilTimeOfDay = (24 - currentHourOfDay) + targetHourOfDay;
						}
						return hoursUntilTimeOfDay;
				}

				public static bool SkippingAhead {
						get {
								return mSkippingAhead;
						}
						set {
								mSkippingAhead = value;
						}
				}

				#endregion

				#region global variables

				#if UNITY_EDITOR
				public double TimeScaleEditor;
				public bool ForceStart = false;
				#endif
				public bool SuspendMessagesThisFrame = false;
				public static System.Diagnostics.Stopwatch StopWatch = new System.Diagnostics.Stopwatch();
				//conversion
				protected readonly static double gSecondsPerRTSecond = 60.0;
				protected readonly static double gMinutesPerRTSecond = 1.0;
				protected readonly static double gHoursPerRTSecond = 1.0 / 60.0;
				protected readonly static double gRTSecondsPerGameSecond = 1.0 / gSecondsPerRTSecond;
				protected readonly static double gRTSecondsPerGameMinute = gRTSecondsPerGameSecond * 60;
				protected readonly static double gRTSecondsPerGameHour = gRTSecondsPerGameMinute * 60;
				//time scales
				public readonly static double gTimeScaleTravel = 5.0;
				public readonly static double gTimeScaleSleep = 15.0;
				public readonly static double gTimeScalePaused = 0.00001;
				public readonly static double gMaxTimeScale = gTimeScaleSleep;
				//real time
				protected static double gHourCycleCurrentART = 0.0;
				protected static double gDayCycleCurrentART = 0.0;
				protected static double gMonthCycleCurrentART = 0.0;
				protected static double gSeasonCycleCurrentRT = 0.0;
				protected static double gYearCycleCurrentART = 0.0;
				protected static double gCenturyCycleCurrentART = 0.0;
				protected static TimeOfDay gTimeOfDayCurrent = TimeOfDay.aa_TimeMidnight;
				protected static TimeOfYear gTimeOfYearCurrent = TimeOfYear.MonthJanuary;
				protected static TimeOfYear gSeasonCurrent = TimeOfYear.SeasonSummer;
				protected static int gDaysSinceBeginningOfTime = 0;
				protected static int gMonthsSinceBeginningOfTime = 0;
				protected static int gYearsSinceBeginningOfTime = 0;

				#endregion

				#region cycles

				//for easy conversion from one game time to another
				//hour cycle
				public readonly static double gMinuteCycleSeconds = 60;
				public readonly static double gHourCycleMinutes = 60;
				public readonly static double gHourCycleSeconds = 3600;
				public readonly static double gHourCycleRT = 60;
				public readonly static double gHourCycleWT = gHourCycleRT * gSecondsPerRTSecond;
				//30;
				//day cycle
				public readonly static double gDayCycleHours = 24;
				public readonly static double gDayCycleMinutes = 1440;
				public readonly static double gDayCycleSeconds = 86400;
				public readonly static double gDayCycleRT = 1440;
				public readonly static double gDayCycleWT = gDayCycleRT * gSecondsPerRTSecond;
				//720;
				//month cycle
				public readonly static double gMonthCycleDays = 30;
				public readonly static double gMonthCycleHours = 720;
				public readonly static double gMonthCycleMinutes = 43200;
				public readonly static double gMonthCycleSeconds = 2592000;
				public readonly static double gMonthCycleRT = 21600;
				public readonly static double gMonthCycleWT = gMonthCycleRT * gSecondsPerRTSecond;
				//season cycle
				public readonly static double gSeasonCycleMonths = 3;
				public readonly static double gSeasonCycleDays = 90;
				public readonly static double gSeasonCycleHours = 2160;
				public readonly static double gSeasonCycleMinutes = 129600;
				public readonly static double gSeasonCycleSeconds = 7776000;
				public readonly static double gSeasonCycleRT = 64800;
				public readonly static double gSeasonCycleWT = gSeasonCycleRT * gSecondsPerRTSecond;
				//year cycle
				public readonly static uint gYearCycleSeasons = 4;
				public readonly static uint gYearCycleMonths = 12;
				public readonly static uint gYearCycleDays = 360;
				public readonly static uint gYearCycleHours = 8640;
				public readonly static uint gYearCycleMinutes = 518400;
				public readonly static uint gYearCycleSeconds = 31104000;
				public readonly static uint gYearCycleRT = 259200;
				public readonly static double gYearCycleWT = gYearCycleRT * gSecondsPerRTSecond;
				//century cycle
				public readonly static uint gCenturyCycleYears = 100;
				public readonly static uint gCenturyCycleSeasons = 400;
				public readonly static uint gCenturyCycleMonths = 1200;
				public readonly static uint gCenturyCycleDays = 36000;
				public readonly static uint gCenturyCycleHours = 864000;
				public readonly static uint gCenturyCycleMinutes = 51840000;
				public readonly static uint gCenturyCycleSeconds = 3110400000;
				public readonly static uint gCenturyCycleRT = 25920000;
				public readonly static double gCenturyCycleWT = gCenturyCycleRT * gSecondsPerRTSecond;

				#endregion

				protected static bool mSkippingAhead = false;
				protected static double mRTime = 0.0f;
				protected static double mRTLastUpdateTime = 0.0f;
				protected static double mRTDeltaTime = 0.0f;
				protected static double mRTDeltaTimeSmooth = 0.0f;
				protected static double mLastARTDeltaTimeAdded = 0.0f;
				protected static double mARTime = 0.0f;
				protected static double mARLastUPdateTime = 0.0f;
				protected static double mARTimeScale = 1.0f;
				protected static double mARTimeScaleTarget = 1.0f;
				protected static double mARTOffsetTime = 0.0f;
				protected static double mARTDeltaTime = 0.0f;
				protected static double mWCTime = 0.0f;
				protected static double mWCDeltaTime = 0.0f;
				protected static double mWCLastUpdatedTime = 0.0f;
				protected static double mTimeScaleChangeSpeed = 0.25f;
				protected static int mLastHour = 0;
				protected static int mNextHour = 0;
				protected static bool mIsDay = false;

				public enum TimeUnit
				{
						Hour,
						Day,
						Week,
						Month,
						Year
				}
		}
}