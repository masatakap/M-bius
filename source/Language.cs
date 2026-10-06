namespace VideoMosaic;
internal static class Language
{
    public static readonly string[] Codes=["ja","en","es","fr","pt","zh"];
    public static readonly string[] Names=["日本語","English","Español","Français","Português","简体中文"];
    public static string Code="ja";
    public static string T(string text)
    {
        string key=Translations.ContainsKey(text)?text:Translations.FirstOrDefault(p=>p.Value.Contains(text)).Key??text;
        int index=Array.IndexOf(Codes,Code);return index<=0||!Translations.TryGetValue(key,out var values)?key:values[index-1];
    }
    public static void Apply(Control control)
    {
        control.Text=T(control.Text);if(control.AccessibleName!=null)control.AccessibleName=T(control.AccessibleName);
        if(control is IconButton icon)icon.RefreshLanguage();
        if(control is ToolStrip strip)foreach(ToolStripItem item in strip.Items)ApplyItem(item);
        if(control is ComboBox combo && combo.Name!="language")for(int i=0;i<combo.Items.Count;i++)combo.Items[i]=T(combo.Items[i]?.ToString()??"");
        foreach(Control child in control.Controls)Apply(child);
    }
    static void ApplyItem(ToolStripItem item){item.Text=T(item.Text??"");if(item is ToolStripDropDownItem menu)foreach(ToolStripItem child in menu.DropDownItems)ApplyItem(child);}
    internal static readonly Dictionary<string,string[]> Translations=Build();
    static Dictionary<string,string[]> Build()
    {
        var result=new Dictionary<string,string[]>();foreach(var line in Data.Split('\n',StringSplitOptions.RemoveEmptyEntries)){var parts=line.TrimEnd('\r').Split('|');if(parts.Length!=6)throw new Exception("Invalid translation row: "+line);result.Add(parts[0].Replace("\\n","\n"),parts.Skip(1).Select(s=>s.Replace("\\n","\n")).ToArray());}return result;
    }
    const string Data="""
    メニュー|Menu|Menú|Menu|Menu|菜单
    メニュー・再生バーを常に表示|Always show menus and playback controls|Mostrar siempre los menús y controles|Toujours afficher les menus et commandes|Mostrar sempre os menus e controlos|始终显示菜单和播放控制栏
    閉じる|Close|Cerrar|Fermer|Fechar|关闭
    最小化|Minimize|Minimizar|Réduire|Minimizar|最小化
    最大化 / 元に戻す|Maximize / restore|Maximizar / restaurar|Agrandir / restaurer|Maximizar / restaurar|最大化 / 还原
    既定のアプリ|Default apps|Apps predeterminadas|Applications par défaut|Aplicações predefinidas|默认应用
    拡張子ごとの既定アプリをWindowsで選択|Choose default apps by file type in Windows|Elegir aplicaciones por tipo de archivo en Windows|Choisir les applications par type dans Windows|Escolher aplicações por tipo no Windows|在Windows中按文件类型选择默认应用
    既定のアプリに設定するには、先にインストーラーでMöbiusをインストールしてください。|Install Möbius with the installer before setting it as a default app.|Instala Möbius con el instalador antes de configurarlo como predeterminado.|Installez Möbius avec l’installateur avant de le définir par défaut.|Instale o Möbius com o instalador antes de o definir como predefinido.|请先使用安装程序安装Möbius，再将其设为默认应用。
    Windowsの設定を開けませんでした。|Could not open Windows Settings.|No se pudieron abrir los ajustes de Windows.|Impossible d’ouvrir les paramètres Windows.|Não foi possível abrir as definições do Windows.|无法打开Windows设置。
    起動中のMöbiusに接続できませんでした。少し待ってから再度開いてください。|Could not connect to Möbius. Wait a moment and try again.|No se pudo conectar con Möbius. Espera y vuelve a intentarlo.|Connexion à Möbius impossible. Réessayez dans un instant.|Não foi possível ligar ao Möbius. Aguarde e tente novamente.|无法连接到Möbius，请稍后重试。
    ファイル(&F)|&File|&Archivo|&Fichier|&Ficheiro|文件(&F)
    動画を追加…|Add videos…|Añadir vídeos…|Ajouter des vidéos…|Adicionar vídeos…|添加视频…
    フォルダーを追加…|Add folder…|Añadir carpeta…|Ajouter un dossier…|Adicionar pasta…|添加文件夹…
    設定…|Settings…|Ajustes…|Paramètres…|Definições…|设置…
    終了|Exit|Salir|Quitter|Sair|退出
    一覧をクリア|Clear all|Vaciar lista|Vider la liste|Limpar lista|清空列表
    表示(&V)|&View|&Ver|&Affichage|&Ver|视图(&V)
    一覧に戻る|Back to grid|Volver a la cuadrícula|Retour à la grille|Voltar à grelha|返回网格
    映像サイズを大きく  Ctrl＋|Larger videos  Ctrl+|Vídeos más grandes  Ctrl+|Agrandir les vidéos  Ctrl+|Aumentar vídeos  Ctrl+|放大视频  Ctrl+
    映像サイズを小さく  Ctrl－|Smaller videos  Ctrl−|Vídeos más pequeños  Ctrl−|Réduire les vidéos  Ctrl−|Reduzir vídeos  Ctrl−|缩小视频  Ctrl−
    フルスクリーン|Fullscreen|Pantalla completa|Plein écran|Ecrã inteiro|全屏
    自動スクロール 開始 / 停止|Start / stop auto-scroll|Iniciar / detener desplazamiento|Démarrer / arrêter le défilement|Iniciar / parar deslocamento|开始 / 停止自动滚动
    動画・フォルダーをここにドロップ\n\nメニュー → 動画を追加  /  Ctrl+O|Drop videos or folders here\n\nMenu → Add videos / Ctrl+O|Arrastra vídeos o carpetas aquí\n\nMenú → Añadir vídeos / Ctrl+O|Déposez des vidéos ou dossiers ici\n\nMenu → Ajouter des vidéos / Ctrl+O|Arraste vídeos ou pastas para aqui\n\nMenu → Adicionar vídeos / Ctrl+O|将视频或文件夹拖到这里\n\n菜单 → 添加视频 / Ctrl+O
    プレビューする動画を選択|Select videos|Seleccionar vídeos|Sélectionner des vidéos|Selecionar vídeos|选择视频
    動画ファイル|Video files|Archivos de vídeo|Fichiers vidéo|Ficheiros de vídeo|视频文件
    すべてのファイル|All files|Todos los archivos|Tous les fichiers|Todos os ficheiros|所有文件
    動画のあるフォルダーを選択|Select a video folder|Seleccionar carpeta de vídeos|Sélectionner un dossier vidéo|Selecionar pasta de vídeos|选择视频文件夹
    : 見つかりません|: not found|: no encontrado| : introuvable|: não encontrado|：未找到
    再生 / 一時停止（クリックモード）|Play / pause (click mode)|Reproducir / pausar (modo clic)|Lecture / pause (mode clic)|Reproduzir / pausar (modo clique)|播放 / 暂停（点击模式）
    この動画を拡大|Enlarge this video|Ampliar este vídeo|Agrandir cette vidéo|Ampliar este vídeo|放大此视频
    先頭に戻す|Return to start|Volver al inicio|Revenir au début|Voltar ao início|返回开头
    動画情報 / エラー詳細|Video info / error details|Información / detalles del error|Informations / détails de l’erreur|Informações / detalhes do erro|视频信息 / 错误详情
    動画情報|Video information|Información del vídeo|Informations vidéo|Informações do vídeo|视频信息
    一覧から外す|Remove from grid|Quitar de la lista|Retirer de la grille|Remover da grelha|从列表移除
    元のファイルの場所を開く|Show original file in folder|Mostrar archivo original en su carpeta|Afficher le fichier d’origine dans son dossier|Mostrar o ficheiro original na pasta|打开原文件所在位置
    コピー|Copy|Copiar|Copier|Copiar|复制
    元のファイルが見つかりません。移動または削除されている可能性があります。|The original file was not found. It may have been moved or deleted.|No se encuentra el archivo original. Puede haberse movido o eliminado.|Le fichier d’origine est introuvable. Il a peut-être été déplacé ou supprimé.|O ficheiro original não foi encontrado. Pode ter sido movido ou eliminado.|找不到原文件，它可能已被移动或删除。
    元のファイルの場所を開けませんでした。|Could not show the original file in its folder.|No se pudo mostrar el archivo original en su carpeta.|Impossible d’afficher le fichier d’origine dans son dossier.|Não foi possível mostrar o ficheiro original na pasta.|无法打开原文件所在位置。
    ファイルをコピーできませんでした。少し待ってから再度お試しください。|Could not copy the file. Wait a moment and try again.|No se pudo copiar el archivo. Espera un momento y vuelve a intentarlo.|Impossible de copier le fichier. Patientez un instant et réessayez.|Não foi possível copiar o ficheiro. Aguarde um momento e tente novamente.|无法复制文件，请稍后重试。
    読み込みのお知らせ|Loading notice|Aviso de carga|Avis de chargement|Aviso de carregamento|加载提示
    設定を保存できませんでした。\n|Could not save settings.\n|No se pudieron guardar los ajustes.\n|Impossible d’enregistrer les paramètres.\n|Não foi possível guardar as definições.\n|无法保存设置。\n
    設定を保存できませんでした|Could not save settings|No se pudieron guardar los ajustes|Impossible d’enregistrer les paramètres|Não foi possível guardar as definições|无法保存设置
    再生位置|Playback position|Posición de reproducción|Position de lecture|Posição de reprodução|播放位置
    音量|Volume|Volumen|Volume|Volume|音量
    再生 / 一時停止（Space）|Play / pause (Space)|Reproducir / pausar (Espacio)|Lecture / pause (Espace)|Reproduzir / pausar (Espaço)|播放 / 暂停（空格）
    先頭は0。番号を入力しEnterで移動・一時停止します。|Starts at 0. Enter a number and press Enter to seek and pause.|Empieza en 0. Introduzca un número y pulse Enter para ir y pausar.|Commence à 0. Saisissez un numéro et appuyez sur Entrée pour aller à cette image et mettre en pause.|Começa em 0. Introduza um número e prima Enter para avançar e pausar.|从0开始。输入帧号并按Enter跳转并暂停。
    フレームレートを取得できません。|Frame rate is unavailable.|La frecuencia de fotogramas no está disponible.|La fréquence d’images est indisponible.|A taxa de fotogramas não está disponível.|无法获取帧率。
    範囲内の整数を入力してください。|Enter an integer within the range.|Introduzca un entero dentro del intervalo.|Saisissez un entier dans la plage indiquée.|Introduza um número inteiro dentro do intervalo.|请输入范围内的整数。
    停止|Stop|Detener|Arrêter|Parar|停止
    1フレーム戻る（Ctrl+←）|Previous frame (Ctrl+←)|Fotograma anterior (Ctrl+←)|Image précédente (Ctrl+←)|Fotograma anterior (Ctrl+←)|上一帧（Ctrl+←）
    1フレーム進む（Ctrl+→）|Next frame (Ctrl+→)|Fotograma siguiente (Ctrl+→)|Image suivante (Ctrl+→)|Fotograma seguinte (Ctrl+→)|下一帧（Ctrl+→）
    音声オン / オフ|Audio on / off|Activar / desactivar audio|Activer / couper le son|Ativar / desativar áudio|开启 / 关闭声音
    音量を保存できませんでした|Could not save volume|No se pudo guardar el volumen|Impossible d’enregistrer le volume|Não foi possível guardar o volume|无法保存音量
    設定 — Möbius|Settings — Möbius|Ajustes — Möbius|Paramètres — Möbius|Definições — Möbius|设置 — Möbius
    プレビューの設定|Preview settings|Ajustes de vista previa|Paramètres d’aperçu|Definições de pré-visualização|预览设置
    再生モード|Playback mode|Modo de reproducción|Mode de lecture|Modo de reprodução|播放模式
    クリックした動画のみ再生|Play clicked video|Reproducir vídeo pulsado|Lire la vidéo sélectionnée|Reproduzir vídeo selecionado|播放点击的视频
    表示中のすべてを再生|Play all visible videos|Reproducir vídeos visibles|Lire les vidéos visibles|Reproduzir vídeos visíveis|播放所有可见视频
    マウスオーバーで再生|Play on hover|Reproducir al pasar el ratón|Lire au survol|Reproduzir ao passar o rato|悬停时播放
    動画間の余白（px）|Video gap (px)|Separación (px)|Espacement (px)|Espaçamento (px)|视频间距（px）
    映像サイズ（高さpx）|Video height (px)|Altura del vídeo (px)|Hauteur vidéo (px)|Altura do vídeo (px)|视频高度（px）
    Ctrl＋で大きく / Ctrl－で小さく（比率は元映像に合わせる）|Ctrl+ larger / Ctrl− smaller; original aspect ratio|Ctrl+ ampliar / Ctrl− reducir; proporción original|Ctrl+ agrandir / Ctrl− réduire ; proportions d’origine|Ctrl+ ampliar / Ctrl− reduzir; proporção original|Ctrl+ 放大 / Ctrl− 缩小；保持原始比例
    ファイル名・解像度などの情報を表示|Show filename and resolution|Mostrar nombre y resolución|Afficher le nom et la résolution|Mostrar nome e resolução|显示文件名和分辨率
    動画のアウトラインを表示|Show video outlines|Mostrar bordes de vídeo|Afficher les contours|Mostrar contornos dos vídeos|显示视频边框
    拡大中の動画だけ音声を再生|Audio for enlarged video only|Audio solo del vídeo ampliado|Son de la vidéo agrandie uniquement|Áudio apenas do vídeo ampliado|仅播放放大视频的声音
    手動操作中は静止画、終了から約200ms後に再開。\n自動スクロール中は再生を続けます（Ctrl+Eで切替）。\n画面外の動画は停止します。|Manual movement pauses videos; resume after 200 ms.\nAuto-scroll keeps playing (Ctrl+E).\nOffscreen videos are paused.|El movimiento manual pausa; reanuda tras 200 ms.\nEl desplazamiento automático mantiene la reproducción (Ctrl+E).\nLos vídeos fuera de pantalla se pausan.|Pause pendant le déplacement manuel, reprise après 200 ms.\nLa lecture continue en défilement automatique (Ctrl+E).\nLes vidéos hors écran sont en pause.|O movimento manual pausa; retoma após 200 ms.\nA reprodução continua no deslocamento automático (Ctrl+E).\nOs vídeos fora do ecrã ficam em pausa.|手动移动时暂停，结束约200毫秒后恢复。\n自动滚动时继续播放（Ctrl+E切换）。\n屏幕外的视频暂停。
    保存|Save|Guardar|Enregistrer|Guardar|保存
    キャンセル|Cancel|Cancelar|Annuler|Cancelar|取消
    自動スクロール速度（px/秒）|Auto-scroll speed (px/s)|Velocidad automática (px/s)|Vitesse de défilement (px/s)|Velocidade automática (px/s)|自动滚动速度（px/秒）
    言語|Language|Idioma|Langue|Idioma|语言
    すべて再生|Play all|Reproducir todos|Tout lire|Reproduzir todos|全部播放
    ホバーで再生|Hover playback|Reproducir al pasar el ratón|Lecture au survol|Reproduzir ao passar o rato|悬停播放
    クリックで再生|Click playback|Reproducir al hacer clic|Lecture au clic|Reproduzir ao clicar|点击播放
    自動移動中|Auto-scroll|Desplazamiento automático|Défilement automatique|Deslocamento automático|自动滚动中
    動画|videos|vídeos|vidéos|vídeos|个视频
    サイズ|Size|Tamaño|Taille|Tamanho|尺寸
    再生中|playing|en reproducción|en lecture|em reprodução|正在播放
    準備中|Preparing|Preparando|Préparation|A preparar|准备中
    映像トラックがありません。|No video track.|No hay pista de vídeo.|Aucune piste vidéo.|Sem faixa de vídeo.|没有视频轨道。
    GPU描画を開始できませんでした。\n|Could not start GPU rendering.\n|No se pudo iniciar la GPU.\n|Impossible de démarrer le rendu GPU.\n|Não foi possível iniciar a GPU.\n|无法启动GPU渲染。\n
    GPU描画面を作成できませんでした。|Could not create GPU surface.|No se pudo crear la superficie GPU.|Impossible de créer la surface GPU.|Não foi possível criar a superfície GPU.|无法创建GPU绘图表面。
    OpenGLを初期化できませんでした。|Could not initialize OpenGL.|No se pudo iniciar OpenGL.|Impossible d’initialiser OpenGL.|Não foi possível iniciar o OpenGL.|无法初始化OpenGL。
    GPUのOpenGLドライバーが利用できません。|GPU OpenGL driver unavailable.|Controlador OpenGL no disponible.|Pilote OpenGL indisponible.|Controlador OpenGL indisponível.|GPU的OpenGL驱动不可用。
    GPU機能が不足しています: |Missing GPU feature: |Función GPU no disponible: |Fonction GPU manquante : |Função GPU em falta: |缺少GPU功能：
    GPU描画を終了しました。|GPU renderer stopped.|Renderizado GPU detenido.|Rendu GPU arrêté.|Renderização GPU parada.|GPU渲染已停止。
    GPU描画の初期化に失敗しました: |GPU initialization failed: |Error al iniciar GPU: |Échec d’initialisation GPU : |Falha ao iniciar GPU: |GPU初始化失败：
    GPU映像バッファを作成できませんでした。|Could not create GPU video buffer.|No se pudo crear el búfer GPU.|Impossible de créer le tampon GPU.|Não foi possível criar o buffer GPU.|无法创建GPU视频缓冲区。
    GPU映像の描画に失敗しました: |GPU rendering failed: |Error de renderizado GPU: |Échec du rendu GPU : |Falha de renderização GPU: |GPU渲染失败：
    再生エンジンを作成できませんでした。|Could not create playback engine.|No se pudo crear el reproductor.|Impossible de créer le moteur de lecture.|Não foi possível criar o leitor.|无法创建播放引擎。
    再生できません|Cannot play|No se puede reproducir|Lecture impossible|Não é possível reproduzir|无法播放
    エラーが発生しました。\n|An error occurred.\n|Se ha producido un error.\n|Une erreur est survenue.\n|Ocorreu um erro.\n|发生错误。\n
    再生エンジン libmpv-2.dll が見つかりません。配布フォルダーをまとめて展開してください。|libmpv-2.dll is missing. Extract the entire app folder.|Falta libmpv-2.dll. Extraiga toda la carpeta.|libmpv-2.dll est absent. Extrayez tout le dossier.|Falta libmpv-2.dll. Extraia a pasta completa.|缺少libmpv-2.dll，请完整解压应用文件夹。
    """;
}


