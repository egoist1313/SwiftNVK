;;; nvk-load.lsp — NETLOAD одной Nvk.Cad.dll.
;;; Команды, конвейер, NOD, панель — в DLL.
;;; (load "E:/Новая папка/Lich15/Поперечник НВК/src/lisp/nvk-load.lsp")
;;; Можно сразу NETLOAD bin\v193\Nvk.Cad.dll без Lisp.

(vl-load-com)

(setq *nvk-dir* nil)

(if (findfile "nvk-load.lsp")
  (setq *nvk-dir* (vl-filename-directory (findfile "nvk-load.lsp"))))

(if (and (not *nvk-dir*)
         (findfile (strcat (getvar "DWGPREFIX") "nvk-load.lsp")))
  (setq *nvk-dir* (getvar "DWGPREFIX")))

(if (and (not *nvk-dir*)
         (findfile "E:\\Новая папка\\Lich15\\Поперечник НВК\\src\\lisp\\nvk-load.lsp"))
  (setq *nvk-dir* "E:\\Новая папка\\Lich15\\Поперечник НВК"))

(if (not *nvk-dir*)
  (progn
    (princ "\n[НВК] укажи nvk-load.lsp в диалоге...")
    (setq _nvk-tmp (getfiled "Укажи nvk-load.lsp" "" "lsp" 0))
    (if _nvk-tmp
      (setq *nvk-dir* (vl-filename-directory _nvk-tmp)))))

(if (not *nvk-dir*)
  (progn
    (princ "\n[НВК] папка модулей не найдена.")
    (princ)
    (exit)))

(if (wcmatch (strcase *nvk-dir*) "*\\SRC\\LISP")
  (setq *nvk-dir*
    (vl-filename-directory (vl-filename-directory *nvk-dir*))))

(while (member (substr *nvk-dir* (strlen *nvk-dir*) 1) '("\\" "/"))
  (setq *nvk-dir* (substr *nvk-dir* 1 (1- (strlen *nvk-dir*)))))

(defun nvk-dll-put ( / kandidaty p k)
  (setq kandidaty
    (list
      (strcat *nvk-dir* "\\bin\\v205\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v204\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v203\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v202\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v201\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v200\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v199\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v198\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v197\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v194\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v193\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v192\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v191\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v190\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v189\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v188\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v187\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v186\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v185\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v184\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v180\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v179\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v178\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v177\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v176\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v175\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v174\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v173\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v172\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v171\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v170\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v169\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v168\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v167\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v166\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v165\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v164\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v163\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v162\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v161\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v160\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v159\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v158\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v157\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v156\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v155\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v154\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v153\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v152\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v151\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v150\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v149\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v148\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v147\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v146\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v145\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v144\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v143\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v142\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\bin\\v141\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\src\\csharp\\Nvk.Cad.2025\\out_build\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\src\\csharp\\Nvk.Cad.2025\\bin\\Release\\net8.0-windows\\Nvk.Cad.dll")
      (strcat *nvk-dir* "\\src\\csharp\\Nvk.Cad.2025\\bin\\Debug\\net8.0-windows\\Nvk.Cad.dll")))
  (setq p nil)
  (foreach k kandidaty
    (if (and (null p) (findfile k))
      (setq p k)))
  p)

(defun nvk-ping-est-p ( / a)
  (setq a (atoms-family 1 '("NVK-PING")))
  (and a (car a) T))

(defun nvk-ping-stroka ( / r)
  (if (not (nvk-ping-est-p))
    ""
    (progn
      (setq r (vl-catch-all-apply 'nvk-ping nil))
      (cond
        ((vl-catch-all-error-p r) "")
        ((null r) "")
        (t (vl-princ-to-string r))))))

(defun nvk-dll-svezhaya-p (s)
  (and s (= (type s) 'STR) (vl-string-search "0.205" s)))

(defun nvk-dll-staraa-preduprezhdenie (s)
  (princ (strcat "\n[НВК] в памяти: " (if (and s (> (strlen s) 0)) s "старая DLL")))
  (princ "\n[НВК] ================================================")
  (princ "\n[НВК]  НУЖНА 0.205. Закрой Civil 3D ПОЛНОСТЬЮ")
  (princ "\n[НВК]  (Файл → Выход), открой чертёж снова,")
  (princ "\n[НВК]  потом (load \"nvk-load.lsp\")")
  (princ "\n[НВК]  NETLOAD на уже загруженную DLL не подменяет.")
  (princ "\n[НВК] ================================================")
  nil)

(defun nvk-netload ( / put old-fdia old-echo err s)
  (if (nvk-ping-est-p)
    (progn
      (setq s (nvk-ping-stroka))
      (if (nvk-dll-svezhaya-p s)
        (progn (princ (strcat "\n[НВК] DLL на связи: " s)) T)
        (nvk-dll-staraa-preduprezhdenie s)))
    (progn
      (setq put (nvk-dll-put))
      (if (null put)
        (progn
          (princ "\n[НВК] Nvk.Cad.dll не найдена. Собери: dotnet build src\\csharp\\Nvk.sln -c Release")
          nil)
        (progn
          (princ (strcat "\n[НВК] NETLOAD " put))
          (setq old-fdia (getvar "FILEDIA")
                old-echo (getvar "CMDECHO"))
          (setvar "FILEDIA" 0)
          (setvar "CMDECHO" 0)
          (setq err
            (if (and (boundp 'command-s) (car (atoms-family 1 '("COMMAND-S"))))
              (vl-catch-all-apply 'command-s (list "._NETLOAD" put))
              (vl-catch-all-apply 'vl-cmdf (list "._NETLOAD" put))))
          (setvar "FILEDIA" old-fdia)
          (setvar "CMDECHO" old-echo)
          (if (vl-catch-all-error-p err)
            (princ (strcat "\n[НВК] NETLOAD ошибка: " (vl-catch-all-error-message err))))
          (if (nvk-ping-est-p)
            (progn
              (if (car (atoms-family 1 '("NVK-ROOT")))
                (vl-catch-all-apply 'nvk-root (list *nvk-dir*)))
              (setq s (nvk-ping-stroka))
              (if (nvk-dll-svezhaya-p s)
                (progn (princ (strcat "\n[НВК] DLL на связи: " s)) T)
                (nvk-dll-staraa-preduprezhdenie s)))
            (progn
              (princ "\n[НВК] NETLOAD не подхватился. Закрой Civil и load снова, либо NETLOAD вручную.")
              nil)))))))

(defun C:НВК-DLL ( / )
  (nvk-netload)
  (princ)
)

(princ (strcat "\n[НВК] корень: " *nvk-dir*))
(nvk-netload)

(princ "\n")
(princ "\n╔══════════════════════════════════════════════╗")
(princ "\n║  Поперечник НВК  0.205  одна DLL             ║")
(princ "\n║  команда  НВК-ПАНЕЛЬ  (или НВК-ИНФО)         ║")
(princ (strcat "\n║  лог  " *nvk-dir* "\\nvk-log.txt"))
(princ "\n╚══════════════════════════════════════════════╝")
(princ)
