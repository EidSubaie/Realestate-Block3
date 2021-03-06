using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Lang;
using Java.Util;
using Java.Util.Concurrent;
using Xamarin.Forms;
using static Android.OS.PowerManager;
using Size = Android.Util.Size;

//todo: log errors in this file

namespace ManageGo.Droid
{

    public class CamRecorder : FrameLayout
    {
        private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();
        public static readonly int REQUEST_CAMERA_PERMISSION = 1;

        public event EventHandler<byte[]> Photo;
        public event EventHandler<string> Video;
        public int SurfaceWidth;
        public int SurfaceHeight;
        WakeLock wakeLock;
        string videoFilePath;
        private Size videoSize;
        // Tag for the {@link Log}.
        private static readonly string TAG = "Camera2BasicFragment";

        // Camera state: Showing camera preview.
        public const int STATE_PREVIEW = 0;

        // Camera state: Waiting for the focus to be locked.
        public const int STATE_WAITING_LOCK = 1;

        // Camera state: Waiting for the exposure to be precapture state.
        public const int STATE_WAITING_PRECAPTURE = 2;

        //Camera state: Waiting for the exposure state to be something other than precapture.
        public const int STATE_WAITING_NON_PRECAPTURE = 3;

        // Camera state: Picture was taken.
        public const int STATE_PICTURE_TAKEN = 4;

        // Max preview width that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_WIDTH = 1920;

        // Max preview height that is guaranteed by Camera2 API
        private static readonly int MAX_PREVIEW_HEIGHT = 1080;

        // TextureView.ISurfaceTextureListener handles several lifecycle events on a TextureView
        // private Camera2BasicSurfaceTextureListener mSurfaceTextureListener;

        // ID of the current {@link CameraDevice}.
        private string mCameraId;

        // An AutoFitTextureView for camera preview
        private AutoFitTextureView mTextureView;

        // A {@link CameraCaptureSession } for camera preview.
        public CameraCaptureSession mCaptureSession;

        // A reference to the opened CameraDevice
        public CameraDevice mCameraDevice;

        // The size of the camera preview
        private Size mPreviewSize;

        // CameraDevice.StateListener is called when a CameraDevice changes its state
        private CameraStateListener mStateCallback;

        // An additional thread for running tasks that shouldn't block the UI.
        private HandlerThread mBackgroundThread;

        // A {@link Handler} for running tasks in the background.
        public Handler mBackgroundHandler;

        // An {@link ImageReader} that handles still image capture.
        private ImageReader mImageReader;

        // This is the output file for our picture.
        public File mFile;

        // This a callback object for the {@link ImageReader}. "onImageAvailable" will be called when a
        // still image is ready to be saved.
        private ImageAvailableListener mOnImageAvailableListener;

        //{@link CaptureRequest.Builder} for the camera preview
        public CaptureRequest.Builder mPreviewRequestBuilder;

        // {@link CaptureRequest} generated by {@link #mPreviewRequestBuilder}
        public CaptureRequest mPreviewRequest;

        // The current state of camera state for taking pictures.
        public int mState = STATE_PREVIEW;

        // A {@link Semaphore} to prevent the app from exiting before closing the camera.
        public Semaphore mCameraOpenCloseLock = new Semaphore(1);

        // Whether the current camera device supports Flash or not.
        private bool mFlashSupported;

        // Orientation of the camera sensor
        private int mSensorOrientation;

        // A {@link CameraCaptureSession.CaptureCallback} that handles events related to JPEG capture.
        public CameraCaptureListener mCaptureCallback;



        private class ShowToastRunnable : Java.Lang.Object, IRunnable
        {
            private string text;
            private Context context;

            public ShowToastRunnable(Context context, string text)
            {
                this.context = context;
                this.text = text;
            }

            public void Run()
            {
                Toast.MakeText(context, text, ToastLength.Short).Show();
            }
        }



        private static Size ChooseOptimalSize(Size[] choices, int textureViewWidth,
            int textureViewHeight, int maxWidth, int maxHeight, Size aspectRatio)
        {
            // Collect the supported resolutions that are at least as big as the preview Surface
            var bigEnough = new List<Size>();
            // Collect the supported resolutions that are smaller than the preview Surface
            var notBigEnough = new List<Size>();
            int w = aspectRatio.Width;
            int h = aspectRatio.Height;

            for (var i = 0; i < choices.Length; i++)
            {
                Size option = choices[i];
                if ((option.Width <= maxWidth) && (option.Height <= maxHeight) &&
                       option.Height == option.Width * h / w)
                {
                    if (option.Width >= textureViewWidth &&
                        option.Height >= textureViewHeight)
                    {
                        bigEnough.Add(option);
                    }
                    else
                    {
                        notBigEnough.Add(option);
                    }
                }
            }

            // Pick the smallest of those big enough. If there is no one big enough, pick the
            // largest of those not big enough.
            if (bigEnough.Count > 0)
            {
                return (Size)Collections.Min(bigEnough, new CompareSizesByArea());
            }
            else if (notBigEnough.Count > 0)
            {
                return (Size)Collections.Max(notBigEnough, new CompareSizesByArea());
            }
            else
            {
                Log.Error(TAG, "Couldn't find any suitable preview size");
                return choices[0];
            }
        }





        // Sets up member variables related to camera.
        private void SetUpCameraOutputs(int width, int height)
        {

            var manager = (CameraManager)_context.GetSystemService(Context.CameraService);
            try
            {
                for (var i = 0; i < manager.GetCameraIdList().Length; i++)
                {
                    var cameraId = manager.GetCameraIdList()[i];
                    CameraCharacteristics characteristics = manager.GetCameraCharacteristics(cameraId);

                    // We don't use a front facing camera in this sample.
                    var facing = (Integer)characteristics.Get(CameraCharacteristics.LensFacing);
                    if (facing != null && facing == (Integer.ValueOf((int)LensFacing.Front)))
                    {
                        continue;
                    }

                    var map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                    if (map == null)
                    {
                        continue;
                    }

                    // For still image captures, we use the largest available size.
                    Size largest = (Size)Collections.Max(Arrays.AsList(map.GetOutputSizes((int)ImageFormatType.Jpeg)),
                        new CompareSizesByArea());
                    mImageReader = ImageReader.NewInstance(largest.Width, largest.Height, ImageFormatType.Jpeg, /*maxImages*/2);
                    mImageReader.SetOnImageAvailableListener(mOnImageAvailableListener, mBackgroundHandler);
                    videoSize = ChooseVideoSize(map.GetOutputSizes(Class.FromType(typeof(MediaRecorder))));
                    // Find out if we need to swap dimension to get the preview size relative to sensor
                    // coordinate.
                    var windowManager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
                    var displayRotation = windowManager.DefaultDisplay.Rotation;
                    //noinspection ConstantConditions
                    mSensorOrientation = (int)characteristics.Get(CameraCharacteristics.SensorOrientation);
                    bool swappedDimensions = false;
                    switch (displayRotation)
                    {
                        case SurfaceOrientation.Rotation0:
                        case SurfaceOrientation.Rotation180:
                            if (mSensorOrientation == 90 || mSensorOrientation == 270)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        case SurfaceOrientation.Rotation90:
                        case SurfaceOrientation.Rotation270:
                            if (mSensorOrientation == 0 || mSensorOrientation == 180)
                            {
                                swappedDimensions = true;
                            }
                            break;
                        default:
                            Log.Error(TAG, "Display rotation is invalid: " + displayRotation);
                            break;
                    }

                    Android.Graphics.Point displaySize = new Android.Graphics.Point();
                    windowManager.DefaultDisplay.GetSize(displaySize);
                    var rotatedPreviewWidth = width;
                    var rotatedPreviewHeight = height;
                    var maxPreviewWidth = displaySize.X;
                    var maxPreviewHeight = displaySize.Y;
                    if (swappedDimensions)
                    {
                        rotatedPreviewWidth = height;
                        rotatedPreviewHeight = width;
                        maxPreviewWidth = displaySize.Y;
                        maxPreviewHeight = displaySize.X;
                    }
                    if (maxPreviewWidth > MAX_PREVIEW_WIDTH)
                    {
                        maxPreviewWidth = MAX_PREVIEW_WIDTH;
                    }
                    if (maxPreviewHeight > MAX_PREVIEW_HEIGHT)
                    {
                        maxPreviewHeight = MAX_PREVIEW_HEIGHT;
                    }
                    // Danger, W.R.! Attempting to use too large a preview size could  exceed the camera
                    // bus' bandwidth limitation, resulting in gorgeous previews but the storage of
                    // garbage capture data.
                    mPreviewSize = ChooseOptimalSize(map.GetOutputSizes(Class.FromType(typeof(SurfaceTexture))),
                        rotatedPreviewWidth, rotatedPreviewHeight, maxPreviewWidth,
                        maxPreviewHeight, largest);

                    // We fit the aspect ratio of TextureView to the size of preview we picked.
                    var orientation = Resources.Configuration.Orientation;
                    if (orientation == Android.Content.Res.Orientation.Landscape)
                    {
                        mTextureView.SetAspectRatio(mPreviewSize.Width, mPreviewSize.Height);
                    }
                    else
                    {
                        mTextureView.SetAspectRatio(mPreviewSize.Height, mPreviewSize.Width);
                    }
                    // Check if the flash is supported.
                    var available = (Java.Lang.Boolean)characteristics.Get(CameraCharacteristics.FlashInfoAvailable);
                    if (available == null)
                    {
                        mFlashSupported = false;
                    }
                    else
                    {
                        mFlashSupported = (bool)available;
                    }
                    mCameraId = cameraId;
                    return;
                }
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (NullPointerException)
            {
                // Currently an NPE is thrown when the Camera2API is used but not supported on the
                // device this code runs.
                ///ErrorDialog.NewInstance(GetString(Resource.String.camera_error)).Show(ChildFragmentManager, FRAGMENT_DIALOG);
            }
        }

        #region VIDEO RECORDING

        /// <summary>
        /// Starts recording video to media recorder. 
        /// Called from CameraPreviewRenderer class on cam button click 
        /// when video is selected.
        /// </summary>
        internal void StartRecordingVideo()
        {
            StartPreviewForVideo();

            try
            {
                //Start recording
                mediaRecorder.Start();
            }
            catch (IllegalStateException e)
            {
                e.PrintStackTrace();
            }
        }

        public void StopRecordingVideo()
        {
            // Workaround for https://github.com/googlesamples/android-Camera2Video/issues/2
            CloseCameraVideo();
            OpenCamera(mTextureView.Width, mTextureView.Height);
            wakeLock?.Release();
            Video?.Invoke(this, videoFilePath);
        }

        public void CloseCameraVideo()
        {
            try
            {
                mCameraOpenCloseLock.Acquire();
                if (null != mCameraDevice)
                {
                    mCameraDevice.Close();
                    mCameraDevice = null;
                }
                if (null != mediaRecorder)
                {
                    mediaRecorder.Release();
                    mediaRecorder = null;
                }
            }
            catch (InterruptedException)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.");
            }
            finally
            {
                mCameraOpenCloseLock.Release();
            }
        }

        /// <summary>
        ///  Starts preview before recording video.
        /// </summary>
        public void StartPreviewForVideo()
        {
            if (null == mCameraDevice || !mTextureView.IsAvailable || null == mPreviewSize)
                return;

            try
            {
                //wake up the cpu
                PowerManager powerManager = (PowerManager)_context.GetSystemService(Context.PowerService);
                wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "MyApp::MyWakelockTag");
                wakeLock.Acquire();
                SetUpMediaRecorder();
                SurfaceTexture texture = mTextureView.SurfaceTexture;
                //Assert.IsNotNull(texture);
                // texture.SetDefaultBufferSize(CamcorderProfile.Get(CamcorderQuality.High., CamcorderProfile.Get(Height));
                mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Record);
                var surfaces = new List<Surface>();
                var previewSurface = new Surface(texture);
                surfaces.Add(previewSurface);
                mPreviewRequestBuilder.AddTarget(previewSurface);
                var recorderSurface = mediaRecorder.Surface;
                surfaces.Add(recorderSurface);
                mPreviewRequestBuilder.AddTarget(recorderSurface);
                mCameraDevice.CreateCaptureSession(surfaces, new PreviewCaptureStateCallback(this), mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (IOException e)
            {
                e.PrintStackTrace();
            }
        }

        //Update the preview
        public void UpdatePreview()
        {
            if (null == mCameraDevice)
                return;

            try
            {
                setUpCaptureRequestBuilder(mPreviewRequestBuilder);
                HandlerThread thread = new HandlerThread("CameraPreview");
                thread.Start();
                mCaptureSession.SetRepeatingRequest(mPreviewRequestBuilder.Build(), null, mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private void setUpCaptureRequestBuilder(CaptureRequest.Builder builder)
        {
            builder.Set(CaptureRequest.ControlMode, new Java.Lang.Integer((int)ControlMode.Auto));

        }

        private void SetUpMediaRecorder()
        {
            if (null == _context)
                return;
            mediaRecorder = new MediaRecorder();
            //changed this camcorder
            mediaRecorder.SetAudioSource(AudioSource.Camcorder);
            mediaRecorder.SetVideoSource(VideoSource.Surface);
            // mediaRecorder.SetOutputFormat(OutputFormat.Mpeg4);

            //profile should be set after audio and video source and before
            //setting the output file
            //set this for test A
            mediaRecorder.SetProfile(CamcorderProfile.Get(CamcorderQuality.High));

            string localFolder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            videoFilePath = System.IO.Path.Combine(localFolder, $"video_{DateTime.Now.ToString("yyMMdd_hhmmss")}.mp4");


            // var localPath = Android.OS.Environment.ExternalStorageDirectory + "/video1.mp4";
            mediaRecorder.SetOutputFile(videoFilePath);

            //mediaRecorder.SetVideoEncodingBitRate(10000000);
            //mediaRecorder.SetVideoFrameRate(30);

            // Call this after setOutFormat() but before prepare().
            mediaRecorder.SetVideoSize(videoSize.Width, videoSize.Height);

            //mediaRecorder.SetVideoEncoder(VideoEncoder.H264);
            //mediaRecorder.SetAudioEncoder(AudioEncoder.Aac);

            var windowManager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            int rotation = (int)windowManager.DefaultDisplay.Rotation;
            int orientation = ORIENTATIONS.Get(rotation);
            mediaRecorder.SetOrientationHint(orientation);
            mediaRecorder.Prepare();
        }

        private Size ChooseVideoSize(Size[] choices)
        {
            foreach (Size size in choices)
            {
                if (size.Width == size.Height * 4 / 3 && size.Width < 2000)
                    return size;
            }
            Log.Error(TAG, "Couldn't find any suitable video size");
            return choices[choices.Length - 1];
        }

        #endregion

        // Opens the camera specified by {@link Camera2BasicFragment#mCameraId}.
        public void OpenCamera(int width, int height)
        {
            SetUpCameraOutputs(width, height);
            ConfigureTransform(width, height);
            var manager = (CameraManager)_context.GetSystemService(Context.CameraService);
            try
            {
                if (!mCameraOpenCloseLock.TryAcquire(2500, TimeUnit.Milliseconds))
                {
                    throw new RuntimeException("Time out waiting to lock camera opening.");
                }

                manager.OpenCamera(mCameraId, mStateCallback, mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera opening.", e);
            }
        }

        // Closes the current {@link CameraDevice}.
        public void CloseCamera()
        {
            try
            {
                mCameraOpenCloseLock.Acquire();
                if (null != mCaptureSession)
                {
                    mCaptureSession.Close();
                    mCaptureSession = null;
                }
                if (null != mCameraDevice)
                {
                    mCameraDevice.Close();
                    mCameraDevice = null;
                }
                if (null != mImageReader)
                {
                    mImageReader.Close();
                    mImageReader = null;
                }
            }
            catch (InterruptedException e)
            {
                throw new RuntimeException("Interrupted while trying to lock camera closing.", e);
            }
            finally
            {
                mCameraOpenCloseLock.Release();
            }
        }

        // Starts a background thread and its {@link Handler}.
        private void StartBackgroundThread()
        {
            mBackgroundThread = new HandlerThread("CameraBackground");
            mBackgroundThread.Start();
            mBackgroundHandler = new Handler(mBackgroundThread.Looper);
        }

        // Stops the background thread and its {@link Handler}.
        private void StopBackgroundThread()
        {
            mBackgroundThread.QuitSafely();
            try
            {
                mBackgroundThread.Join();
                mBackgroundThread = null;
                mBackgroundHandler = null;
            }
            catch (InterruptedException e)
            {
                e.PrintStackTrace();
            }
        }

        // Creates a new {@link CameraCaptureSession} for camera preview.
        public void CreateCameraPreviewSession(bool forVideo = false)
        {
            //surfacetexture available -> camera opened -> preview for still images starts
            try
            {
                SurfaceTexture texture = mTextureView.SurfaceTexture;
                if (texture == null)
                {
                    throw new IllegalStateException("texture is null");
                }

                // We configure the size of default buffer to be the size of camera preview we want.
                texture.SetDefaultBufferSize(mPreviewSize.Width, mPreviewSize.Height);

                // This is the output Surface we need to start preview.
                Surface surface = new Surface(texture);

                // We set up a CaptureRequest.Builder with the output Surface.
                // if (forVideo)
                //    mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Record);
                // else
                mPreviewRequestBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                mPreviewRequestBuilder.AddTarget(surface);

                // Here, we create a CameraCaptureSession for camera preview.
                List<Surface> surfaces = new List<Surface>
                {
                    surface
                };

                if (forVideo && mediaRecorder != null)
                {
                    surfaces.Add(mediaRecorder.Surface);
                }
                else
                {
                    surfaces.Add(mImageReader.Surface);
                }
                // prepares the camera (focus/flash) and shows the preview for the camera
                // also sets the mCaptureSession for trigerring the capture on takephoto
                //todo: change to continueVideo for video preview
                mCameraDevice.CreateCaptureSession(surfaces, new CameraCaptureSessionCallback(this), null);

            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        public static T Cast<T>(Java.Lang.Object obj) where T : class
        {
            var propertyInfo = obj.GetType().GetProperty("Instance");
            return propertyInfo == null ? null : propertyInfo.GetValue(obj, null) as T;
        }

        // Configures the necessary {@link android.graphics.Matrix}
        // transformation to `mTextureView`.
        // This method should be called after the camera preview size is determined in
        // setUpCameraOutputs and also the size of `mTextureView` is fixed.

        public void ConfigureTransform(int viewWidth, int viewHeight)
        {

            if (null == mTextureView || null == mPreviewSize || null == _context)
            {
                return;
            }
            var windowManager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
            var rotation = (int)windowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix();
            RectF viewRect = new RectF(0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF(0, 0, mPreviewSize.Height, mPreviewSize.Width);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if ((int)SurfaceOrientation.Rotation90 == rotation || (int)SurfaceOrientation.Rotation270 == rotation)
            {
                bufferRect.Offset(centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect(viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = Java.Lang.Math.Max((float)viewHeight / mPreviewSize.Height, (float)viewWidth / mPreviewSize.Width);
                matrix.PostScale(scale, scale, centerX, centerY);
                matrix.PostRotate(90 * (rotation - 2), centerX, centerY);
            }
            else if ((int)SurfaceOrientation.Rotation180 == rotation)
            {
                matrix.PostRotate(180, centerX, centerY);
            }
            mTextureView.SetTransform(matrix);
        }

        // Initiate a still image capture.
        public void TakePicture()
        {
            LockFocus();
        }

        // Lock the focus as the first step for a still image capture.
        private void LockFocus()
        {
            try
            {
                // This is how to tell the camera to lock focus.
                mPreviewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Start);
                // Tell #mCaptureCallback to wait for the lock.
                mState = STATE_WAITING_LOCK;
                mCaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback,
                        mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Run the precapture sequence for capturing a still image. This method should be called when
        // we get a response in {@link #mCaptureCallback} from {@link #lockFocus()}.
        public void RunPrecaptureSequence()
        {
            try
            {
                // This is how to tell the camera to trigger.
                mPreviewRequestBuilder.Set(CaptureRequest.ControlAePrecaptureTrigger, (int)ControlAEPrecaptureTrigger.Start);
                // Tell #mCaptureCallback to wait for the precapture sequence to be set.
                mState = STATE_WAITING_PRECAPTURE;
                mCaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback, mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        private CaptureRequest.Builder stillCaptureBuilder;
        private Context _context;
        private MediaRecorder mediaRecorder;

        public CamRecorder(Context context) : base(context)
        {
            _context = context;
            var inflater = LayoutInflater.FromContext(context);
            if (inflater is null)
                return;
            var view = inflater.Inflate(Resource.Layout.CameraLayout, this);
            mTextureView = view.FindViewById<AutoFitTextureView>(Resource.Id.CameraTexture);

            //1- opens camera when surface is available, also resized the view
            mTextureView.SurfaceTextureListener = new Camera2BasicSurfaceTextureListener(this);

            //2- listens for when camera is opened -> calls CreateCameraPreviewSession
            mStateCallback = new CameraStateListener(this);
            // mSurfaceTextureListener = mTextureView.SurfaceTextureListener;//new Camera2BasicSurfaceTextureListener(this);

            // fill ORIENTATIONS list
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation0, 90);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation90, 0);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation180, 270);
            ORIENTATIONS.Append((int)SurfaceOrientation.Rotation270, 180);
            mCaptureCallback = new CameraCaptureListener(this);
            mOnImageAvailableListener = new ImageAvailableListener();
            mOnImageAvailableListener.Photo += (object sender, byte[] e) =>
            {
                Device.BeginInvokeOnMainThread(() =>
                {
                    Photo?.Invoke(this, e);
                });
            };
        }

        // Capture a still picture. This method should be called when we get a response in
        // {@link #mCaptureCallback} from both {@link #lockFocus()}.
        public void CaptureStillPicture()
        {
            try
            {

                if (null == _context || null == mCameraDevice)
                {
                    return;
                }
                // This is the CaptureRequest.Builder that we use to take a picture.
                if (stillCaptureBuilder == null)
                    stillCaptureBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
                stillCaptureBuilder.AddTarget(mImageReader.Surface);
                // Use the same AE and AF modes as the preview.
                stillCaptureBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                SetAutoFlash(stillCaptureBuilder);
                // Orientation
                var windowManager = _context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>();
                int rotation = (int)windowManager.DefaultDisplay.Rotation;
                stillCaptureBuilder.Set(CaptureRequest.JpegOrientation, GetOrientation(rotation));
                mCaptureSession.StopRepeating();
                mCaptureSession.Capture(stillCaptureBuilder.Build(), new CameraCaptureStillPictureSessionCallback(this), null);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }

        // Retrieves the JPEG orientation from the specified screen rotation.
        private int GetOrientation(int rotation)
        {
            // Sensor orientation is 90 for most devices, or 270 for some devices (eg. Nexus 5X)
            // We have to take that into account and rotate JPEG properly.
            // For devices with orientation of 90, we simply return our mapping from ORIENTATIONS.
            // For devices with orientation of 270, we need to rotate the JPEG 180 degrees.
            return (ORIENTATIONS.Get(rotation) + mSensorOrientation + 270) % 360;
        }

        // Unlock the focus. This method should be called when still image capture sequence is
        // finished.
        public void UnlockFocus()
        {
            try
            {
                // Reset the auto-focus trigger
                mPreviewRequestBuilder.Set(CaptureRequest.ControlAfTrigger, (int)ControlAFTrigger.Cancel);
                SetAutoFlash(mPreviewRequestBuilder);
                mCaptureSession.Capture(mPreviewRequestBuilder.Build(), mCaptureCallback,
                        mBackgroundHandler);
                // After this, the camera will go back to the normal state of preview.
                mState = STATE_PREVIEW;
                mCaptureSession.SetRepeatingRequest(mPreviewRequest, mCaptureCallback,
                        mBackgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }



        public void SetAutoFlash(CaptureRequest.Builder requestBuilder)
        {
            if (mFlashSupported)
            {
                requestBuilder.Set(CaptureRequest.ControlAeMode, (int)ControlAEMode.OnAutoFlash);
            }
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height)
        {
            throw new NotImplementedException();
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface)
        {
            throw new NotImplementedException();
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height)
        {
            throw new NotImplementedException();
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture surface)
        {
            throw new NotImplementedException();
        }
    }

}