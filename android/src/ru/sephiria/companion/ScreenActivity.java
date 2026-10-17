package ru.sephiria.companion;

import android.app.Activity;
import android.content.SharedPreferences;
import android.content.Intent;
import android.content.res.Configuration;
import android.graphics.Color;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import java.util.Locale;

public final class ScreenActivity extends Activity {
    private static final String ADDRESS = "http://127.0.0.1:27310/";
    private final Handler handler = new Handler(Looper.getMainLooper());
    private WebView web;
    private LinearLayout startup;
    private TextView status;
    private Button refresh, russian, english;
    private SharedPreferences preferences;
    private volatile String language;
    private boolean failed, loaded;

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        preferences = getSharedPreferences("companion", MODE_PRIVATE);
        language = preferences.getString("language", "ru".equals(Locale.getDefault().getLanguage()) ? "ru" : "en");
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON | WindowManager.LayoutParams.FLAG_FULLSCREEN);
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(32, 30, 44));
        web = new WebView(this);
        web.setBackgroundColor(Color.rgb(32, 30, 44));
        root.addView(web, new FrameLayout.LayoutParams(-1, -1));
        startup = new LinearLayout(this);
        startup.setOrientation(LinearLayout.VERTICAL);
        startup.setGravity(Gravity.CENTER);
        startup.setPadding(24, 14, 24, 14);
        startup.setBackgroundColor(Color.rgb(32, 30, 44));
        TextView title = new TextView(this);
        title.setText("SEPHIRIA COMPANION");
        title.setTextColor(Color.rgb(238, 211, 163));
        title.setTextSize(20);
        title.setGravity(Gravity.CENTER);
        startup.addView(title, new LinearLayout.LayoutParams(-1, -2));
        LinearLayout languages = new LinearLayout(this);
        languages.setGravity(Gravity.CENTER);
        russian = new Button(this); russian.setText("Русский");
        english = new Button(this); english.setText("English");
        russian.setOnClickListener(view -> setLanguage("ru"));
        english.setOnClickListener(view -> setLanguage("en"));
        languages.addView(russian); languages.addView(english);
        startup.addView(languages, new LinearLayout.LayoutParams(-1, -2));
        status = new TextView(this);
        status.setTextColor(Color.rgb(238, 225, 186));
        status.setTextSize(15);
        status.setGravity(Gravity.CENTER);
        startup.addView(status, new LinearLayout.LayoutParams(-1, -2));
        refresh = new Button(this);
        refresh.setOnClickListener(view -> connect());
        startup.addView(refresh, new LinearLayout.LayoutParams(-2, -2));
        root.addView(startup, new FrameLayout.LayoutParams(-1, -1));
        setContentView(root);
        WebSettings settings = web.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setTextZoom(100);
        settings.setSupportZoom(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(false);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setAllowContentAccess(false);
        settings.setJavaScriptCanOpenWindowsAutomatically(false);
        settings.setSupportMultipleWindows(false);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        web.addJavascriptInterface(new LanguagePreferences(), "CompanionPreferences");
        WebView.setWebContentsDebuggingEnabled(false);
        web.setWebViewClient(new WebViewClient() {
            @Override public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) { return !allowed(request.getUrl()); }
            @Override public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                if (allowed(request.getUrl())) return null;
                return new WebResourceResponse("text/plain", "utf-8", new java.io.ByteArrayInputStream(new byte[0]));
            }
            @Override public void onPageFinished(WebView view, String url) {
                if (!failed) { loaded = true; refresh.setEnabled(true); startup.setVisibility(View.GONE); }
            }
            @Override public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request.isForMainFrame()) showFailure();
            }
            @Override public void onReceivedHttpError(WebView view, WebResourceRequest request, WebResourceResponse response) {
                if (request.isForMainFrame()) showFailure();
            }
        });
        updateLanguage();
        // The user chooses the language and connects explicitly. No retry timer,
        // background requests or automatic page reloads run on this screen.
    }

    private boolean allowed(Uri uri) {
        return "http".equals(uri.getScheme()) && "127.0.0.1".equals(uri.getHost()) && uri.getPort() == 27310;
    }

    private void connect() {
        failed = false;
        refresh.setEnabled(false);
        status.setText("ru".equals(language) ? "Подключение к Sephiria…" : "Connecting to Sephiria…");
        web.loadUrl(ADDRESS + "?lang=" + language);
    }

    private void showFailure() {
        failed = true;
        loaded = false;
        refresh.setEnabled(true);
        startup.setVisibility(View.VISIBLE);
        updateLanguage();
    }

    private void setLanguage(String value) {
        if (!"ru".equals(value) && !"en".equals(value)) return;
        language = value;
        preferences.edit().putString("language", value).apply();
        updateLanguage();
    }

    private void updateLanguage() {
        russian.setSelected("ru".equals(language));
        english.setSelected("en".equals(language));
        russian.setTextColor("ru".equals(language) ? Color.rgb(247, 211, 144) : Color.LTGRAY);
        english.setTextColor("en".equals(language) ? Color.rgb(247, 211, 144) : Color.LTGRAY);
        refresh.setText("ru".equals(language) ? "Обновить" : "Refresh");
        status.setText("ru".equals(language)
            ? (failed ? "Игра пока не отвечает.\n\n" : "") + "1. Запусти Sephiria.\n2. Загрузи сохранение и дождись персонажа.\n3. Нажми «Обновить»."
            : (failed ? "The game is not responding yet.\n\n" : "") + "1. Launch Sephiria.\n2. Load a save and wait for your character.\n3. Tap Refresh.");
    }

    public final class LanguagePreferences {
        @JavascriptInterface public String getLanguage() { return language; }
        @JavascriptInterface public void setLanguage(String value) { handler.post(() -> ScreenActivity.this.setLanguage(value)); }
        @JavascriptInterface public void resetViewport() { handler.post(() -> ScreenActivity.this.resetViewport()); }
    }

    private void resetViewport() {
        if (web == null) return;
        web.getSettings().setTextZoom(100);
        web.setInitialScale(0);
        float scale = web.getScale();
        float density = web.getResources().getDisplayMetrics().density;
        if (scale > 0 && Math.abs(scale - density) > .01f) web.zoomBy(density / scale);
        web.setLayoutParams(new FrameLayout.LayoutParams(-1, -1));
        web.requestLayout(); web.invalidate();
        hideSystemBars();
        web.evaluateJavascript("window.dispatchEvent(new Event('resize'));", null);
    }

    @Override public void onConfigurationChanged(Configuration configuration) {
        super.onConfigurationChanged(configuration);
        resetViewport();
    }

    @Override protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        resetViewport();
    }

    @Override protected void onStart() {
        super.onStart();
        if (web != null) {
            web.onResume();
            if (loaded) web.evaluateJavascript("window.dispatchEvent(new Event('companionResumed'));", null);
        }
    }

    @Override protected void onStop() {
        if (web != null) {
            web.evaluateJavascript("window.dispatchEvent(new Event('companionPaused'));", null);
            web.onPause();
        }
        super.onStop();
    }

    @Override public void onWindowFocusChanged(boolean focus) {
        super.onWindowFocusChanged(focus);
        if (!focus) return;
        hideSystemBars();
    }

    private void hideSystemBars() {
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.hide(WindowInsets.Type.systemBars());
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
            }
        } else getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION |
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY | View.SYSTEM_UI_FLAG_LAYOUT_STABLE |
            View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
    }

    @Override protected void onDestroy() {
        handler.removeCallbacksAndMessages(null);
        if (web != null) { web.removeJavascriptInterface("CompanionPreferences"); web.stopLoading(); web.destroy(); }
        super.onDestroy();
    }
}
