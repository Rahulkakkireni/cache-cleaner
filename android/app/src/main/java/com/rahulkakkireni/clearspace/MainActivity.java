package com.rahulkakkireni.clearspace;

import android.app.*;
import android.content.*;
import android.graphics.Color;
import android.net.Uri;
import android.os.*;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import androidx.documentfile.provider.DocumentFile;
import java.io.File;
import java.nio.file.Files;
import java.util.*;
import java.util.concurrent.*;

public class MainActivity extends Activity {
    static class Entry {
        File file; DocumentFile document; long size, modified;
        Entry(File value){file=value;size=value.length();modified=value.lastModified();}
        Entry(DocumentFile value){document=value;size=value.length();modified=value.lastModified();}
        boolean delete(){
            if(file!=null){if(!file.isFile()||Files.isSymbolicLink(file.toPath())||file.length()!=size||file.lastModified()!=modified)return false;return file.delete();}
            return document!=null&&document.isFile()&&document.length()==size&&document.lastModified()==modified&&document.delete();
        }
    }

    final ArrayList<Entry> entries=new ArrayList<>();
    final ExecutorService worker=Executors.newSingleThreadExecutor();
    final Handler main=new Handler(Looper.getMainLooper());
    LinearLayout locations; TextView amount,status,folder; Button scan,clean,choose; Spinner ages; ProgressBar progress;
    Uri selectedTree; long selectedBytes; int skipped,recent;

    int dp(int value){return Math.round(value*getResources().getDisplayMetrics().density);}
    TextView text(String value,float size,boolean bold){TextView v=new TextView(this);v.setText(value);v.setTextSize(size);v.setTextColor(Color.rgb(29,29,31));if(bold)v.setTypeface(null,1);return v;}
    void margin(View v,int top){LinearLayout.LayoutParams p=new LinearLayout.LayoutParams(-1,-2);p.topMargin=dp(top);v.setLayoutParams(p);}

    @Override public void onCreate(Bundle state){super.onCreate(state);
        getWindow().setStatusBarColor(Color.rgb(245,245,247));getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);
        ScrollView scroll=new ScrollView(this);LinearLayout root=new LinearLayout(this);root.setOrientation(LinearLayout.VERTICAL);root.setPadding(dp(22),dp(22),dp(22),dp(28));root.setBackgroundColor(Color.rgb(245,245,247));scroll.addView(root);
        root.addView(text("ClearSpace",30,true));TextView by=text("Android cache cleaner  •  Made by Rahul Kakkireni",13,false);by.setTextColor(Color.DKGRAY);root.addView(by);
        TextView notice=text("Android protects other apps' private caches. ClearSpace safely cleans its own cache and a shared folder you choose.",14,false);notice.setPadding(0,dp(18),0,dp(12));root.addView(notice);

        ages=new Spinner(this);String[] choices={"Older than 24 hours","Older than 7 days","Older than 30 days","Any age"};ages.setAdapter(new ArrayAdapter<>(this,android.R.layout.simple_spinner_dropdown_item,choices));root.addView(ages);
        choose=new Button(this);choose.setText("Choose shared folder");margin(choose,12);root.addView(choose);
        folder=text("No shared folder selected",13,false);folder.setPadding(dp(4),dp(6),dp(4),dp(12));root.addView(folder);

        locations=new LinearLayout(this);locations.setOrientation(LinearLayout.VERTICAL);locations.setPadding(dp(16),dp(12),dp(16),dp(12));locations.setBackgroundColor(Color.WHITE);root.addView(locations,new LinearLayout.LayoutParams(-1,-2));
        addLocation("ClearSpace internal cache","Always available; no storage permission required");
        addLocation("ClearSpace external cache","Available when device storage is mounted");
        addLocation("User-selected shared folder","Only the folder explicitly granted through Android's picker");

        amount=text("Ready to scan",26,true);amount.setPadding(0,dp(20),0,dp(4));root.addView(amount);
        status=text("Choose an age and scan. Nothing is deleted during scanning.",14,false);root.addView(status);
        progress=new ProgressBar(this);progress.setIndeterminate(true);progress.setVisibility(View.GONE);margin(progress,10);root.addView(progress);
        LinearLayout actions=new LinearLayout(this);actions.setOrientation(LinearLayout.HORIZONTAL);actions.setGravity(Gravity.END);actions.setPadding(0,dp(18),0,0);root.addView(actions);
        scan=new Button(this);scan.setText("Scan Android");actions.addView(scan);
        clean=new Button(this);clean.setText("Clean selected");clean.setEnabled(false);actions.addView(clean);
        setContentView(scroll);

        String saved=getPreferences(MODE_PRIVATE).getString("tree",null);if(saved!=null){selectedTree=Uri.parse(saved);folder.setText("Selected folder: "+selectedTree);}
        choose.setOnClickListener(v->chooseFolder());scan.setOnClickListener(v->scan());clean.setOnClickListener(v->confirmClean());
    }

    void addLocation(String name,String detail){TextView v=text(name+"\n"+detail,15,true);v.setPadding(0,dp(8),0,dp(8));locations.addView(v);}
    long cutoff(){int i=ages.getSelectedItemPosition();if(i==3)return Long.MAX_VALUE;long days=i==0?1:i==1?7:30;return System.currentTimeMillis()-days*86400000L;}
    void busy(boolean value,String message){scan.setEnabled(!value);choose.setEnabled(!value);clean.setEnabled(!value&&entries.size()>0);progress.setVisibility(value?View.VISIBLE:View.GONE);status.setText(message);}

    void chooseFolder(){Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION|Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);startActivityForResult(intent,42);}
    @Override protected void onActivityResult(int request,int result,Intent data){super.onActivityResult(request,result,data);if(request==42&&result==RESULT_OK&&data!=null&&data.getData()!=null){selectedTree=data.getData();int flags=data.getFlags()&(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION);try{getContentResolver().takePersistableUriPermission(selectedTree,flags);}catch(Exception ignored){}getPreferences(MODE_PRIVATE).edit().putString("tree",selectedTree.toString()).apply();folder.setText("Selected folder: "+selectedTree);entries.clear();clean.setEnabled(false);amount.setText("Scan to refresh");}}

    void scan(){busy(true,"Scanning accessible cache and shared files…");entries.clear();selectedBytes=0;skipped=0;recent=0;long limit=cutoff();worker.execute(()->{HashSet<String> seen=new HashSet<>();scanFileRoot(getCacheDir(),limit,seen);for(File root:getExternalCacheDirs())if(root!=null)scanFileRoot(root,limit,seen);if(selectedTree!=null){DocumentFile root=DocumentFile.fromTreeUri(this,selectedTree);if(root!=null)scanDocument(root,limit);}main.post(()->{amount.setText(entries.size()+" files • "+format(selectedBytes));busy(false,"Scan complete. "+recent+" recent and "+skipped+" inaccessible/unsafe files skipped.");clean.setEnabled(!entries.isEmpty());});});}
    void scanFileRoot(File root,long limit,HashSet<String> seen){try{if(root==null||!root.exists()||Files.isSymbolicLink(root.toPath()))return;String base=root.getCanonicalPath()+File.separator;ArrayDeque<File> pending=new ArrayDeque<>();pending.push(root);while(!pending.isEmpty()){File item=pending.pop();File[] children=item.listFiles();if(children==null){skipped++;continue;}for(File child:children){try{if(Files.isSymbolicLink(child.toPath())){skipped++;continue;}String path=child.getCanonicalPath();if(!path.startsWith(base)){skipped++;continue;}if(child.isDirectory())pending.push(child);else if(child.isFile()&&seen.add(path)){if(child.lastModified()>=limit){recent++;continue;}Entry e=new Entry(child);entries.add(e);selectedBytes+=e.size;}}catch(Exception error){skipped++;}}}}catch(Exception error){skipped++;}}
    void scanDocument(DocumentFile root,long limit){ArrayDeque<DocumentFile> pending=new ArrayDeque<>();pending.push(root);while(!pending.isEmpty()){DocumentFile item=pending.pop();DocumentFile[] children;try{children=item.listFiles();}catch(Exception error){skipped++;continue;}for(DocumentFile child:children){try{if(child.isDirectory())pending.push(child);else if(child.isFile()){if(child.lastModified()>=limit){recent++;continue;}Entry e=new Entry(child);entries.add(e);selectedBytes+=e.size;}}catch(Exception error){skipped++;}}}}

    void confirmClean(){new AlertDialog.Builder(this).setTitle("Permanently delete selected files?").setMessage(entries.size()+" files ("+format(selectedBytes)+") will be removed. Changed, inaccessible and protected files are skipped.").setNegativeButton("Cancel",null).setPositiveButton("Delete",(d,w)->clean()).show();}
    void clean(){busy(true,"Deleting eligible files…");ArrayList<Entry> snapshot=new ArrayList<>(entries);worker.execute(()->{int deleted=0,failed=0;long bytes=0;for(Entry e:snapshot){if(e.delete()){deleted++;bytes+=e.size;}else failed++;}int finalDeleted=deleted,finalFailed=failed;long finalBytes=bytes;main.post(()->{entries.clear();selectedBytes=0;amount.setText("Removed "+format(finalBytes));busy(false,"Finished. "+finalDeleted+" files removed; "+finalFailed+" skipped. Scan again to refresh.");clean.setEnabled(false);});});}
    String format(long bytes){String[] units={"B","KB","MB","GB"};double value=bytes;int unit=0;while(value>=1024&&unit<units.length-1){value/=1024;unit++;}return String.format(Locale.getDefault(),unit==0?"%.0f %s":"%.1f %s",value,units[unit]);}
    @Override protected void onDestroy(){worker.shutdownNow();super.onDestroy();}
}
