package com.ARLibraryNav.mlkit;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;

import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.text.TextRecognition;
import com.google.mlkit.vision.text.TextRecognizer;
import com.google.mlkit.vision.text.latin.TextRecognizerOptions;
import com.unity3d.player.UnityPlayer;

/**
 * Android ML Kit Text Recognition bridge for Unity.
 *
 * Called from C# via AndroidJavaClass.CallStatic("readText", jpegBytes, callbackObject).
 * Results are returned to Unity via UnitySendMessage on the provided callbackObject name.
 *
 * Success → callbackObject.OnMLKitSuccess(extractedText)
 * Failure → callbackObject.OnMLKitFailure(errorMessage)
 */
public class ShelfLabelReader {

    /**
     * Runs ML Kit text recognition on a JPEG byte array.
     *
     * @param jpegBytes      Raw JPEG bytes from Unity's camera capture.
     * @param callbackObject Name of the Unity GameObject that will receive the result.
     *                       Must have OnMLKitSuccess(String) and OnMLKitFailure(String) methods.
     */
    public static void readText(byte[] jpegBytes, String callbackObject) {
        try {
            // Decode JPEG bytes to a Bitmap
            Bitmap bitmap = BitmapFactory.decodeByteArray(jpegBytes, 0, jpegBytes.length);
            if (bitmap == null) {
                UnityPlayer.UnitySendMessage(callbackObject, "OnMLKitFailure",
                        "Failed to decode JPEG image.");
                return;
            }

            // Wrap in ML Kit InputImage (rotation 0 — Unity camera is already upright)
            InputImage image = InputImage.fromBitmap(bitmap, 0);

            // Run text recognition with the default Latin script recogniser
            TextRecognizer recognizer = TextRecognition.getClient(
                    TextRecognizerOptions.DEFAULT_OPTIONS);

            recognizer.process(image)
                    .addOnSuccessListener(visionText -> {
                        String text = visionText.getText();
                        if (text == null || text.trim().isEmpty()) {
                            UnityPlayer.UnitySendMessage(callbackObject, "OnMLKitFailure",
                                    "No text found in image.");
                        } else {
                            UnityPlayer.UnitySendMessage(callbackObject, "OnMLKitSuccess",
                                    text.trim());
                        }
                    })
                    .addOnFailureListener(e -> {
                        String msg = e.getMessage();
                        UnityPlayer.UnitySendMessage(callbackObject, "OnMLKitFailure",
                                msg != null ? msg : "ML Kit recognition failed.");
                    });

        } catch (Exception e) {
            String msg = e.getMessage();
            UnityPlayer.UnitySendMessage(callbackObject, "OnMLKitFailure",
                    msg != null ? msg : "Unexpected error in ShelfLabelReader.");
        }
    }
}
