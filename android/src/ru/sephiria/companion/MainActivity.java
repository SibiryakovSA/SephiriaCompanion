package ru.sephiria.companion;
import android.app.Activity;
import android.app.ActivityOptions;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.os.Bundle;
import android.view.Display;
import android.widget.Toast;
import java.util.Locale;

public final class MainActivity extends Activity {
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        DisplayManager manager = (DisplayManager)getSystemService(DISPLAY_SERVICE);
        Display target = secondaryDisplay(manager);
        Intent intent = new Intent(this, ScreenActivity.class);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        try {
            if (target != null) {
                ActivityOptions options = ActivityOptions.makeBasic();
                options.setLaunchDisplayId(target.getDisplayId());
                startActivity(intent, options.toBundle());
            } else startActivity(intent);
        } catch (RuntimeException failure) {
            String language = getSharedPreferences("companion", MODE_PRIVATE).getString("language", Locale.getDefault().getLanguage());
            Toast.makeText(this, "ru".equals(language)
                ? "Перемести компаньон на нижний экран через меню AYN"
                : "Move the companion to the lower screen using the AYN menu", Toast.LENGTH_LONG).show();
            startActivity(intent);
        }
        finish();
    }

    private static Display secondaryDisplay(DisplayManager manager) {
        Display target = findSecondary(manager.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION));
        return target != null ? target : findSecondary(manager.getDisplays());
    }

    private static Display findSecondary(Display[] displays) {
        for (Display display : displays)
            if (display.getDisplayId() != Display.DEFAULT_DISPLAY && display.getState() != Display.STATE_OFF) return display;
        return null;
    }
}
