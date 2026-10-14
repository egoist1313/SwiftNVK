;;; nvk-input.lsp — этап INPUT: трубы + TIN. Ничего не рисует.
;;; Зависит от: nvk-config, nvk-dll.

(if (not (boundp '*nvk-pipe-ss*))     (setq *nvk-pipe-ss* nil))
(if (not (boundp '*nvk-surf-handle*)) (setq *nvk-surf-handle* nil))
(if (not (boundp '*nvk-surf-name*))   (setq *nvk-surf-name* nil))
(if (not (boundp '*nvk-stations-str*)) (setq *nvk-stations-str* "AUTO"))
(setq *nvk-last-dump*   nil)
(setq *nvk-input-sprosit* nil)

(defun nvk-input-est-p ( / )
  (and *nvk-pipe-ss* (> (sslength *nvk-pipe-ss*) 0)))

(defun nvk-stage-input ( / ss n)
  ;; ПРОГОН не переспрашивает, если набор уже есть. НВК-ВВОД ставит *nvk-input-sprosit*.
  (if (and (nvk-input-est-p) (not *nvk-input-sprosit*))
    (progn
      (princ (strcat "\n[НВК] INPUT: труб " (itoa (sslength *nvk-pipe-ss*))
                     "  TIN=" (if *nvk-surf-name* *nvk-surf-name* "-")
                     "  (НВК-ВВОД — сменить)"))
      T)
    (progn
      (princ "\n[НВК] INPUT: выбери трубы Civil 3D")
      (if (nvk-input-est-p)
        (princ (strcat "  <Enter = оставить " (itoa (sslength *nvk-pipe-ss*)) ">")))
      (setq ss (ssget '((0 . "AECC_PIPE"))))
      (cond
        ((null ss)
         (if (nvk-input-est-p)
           (progn
             (princ (strcat "\n[НВК] оставляю прежние трубы: "
                            (itoa (sslength *nvk-pipe-ss*))))
             T)
           (progn (princ "\n[НВК] трубы не выбраны.") nil)))
        (t
         (setq *nvk-pipe-ss* ss)
         (setq n (sslength ss))
         (princ (strcat "\n[НВК] труб: " (itoa n)))
         (if (nvk-nod-podhvat-po-trubam)
           (princ "\n[НВК] подхвачен сохранённый участок этих труб")
           (setq *nvk-cs-ins* nil  *nvk-ep-ins* nil  *nvk-vol-ins* nil
                 *nvk-cs-lpts* (list nil nil nil nil nil)
                 *nvk-ep-lpts* (list nil nil nil nil nil)))
         (nvk-vybrat-tin)
         (vl-catch-all-apply 'nvk-nod-sohranit nil)
         T)))))

(defun nvk-vybrat-tin ( / lst)
  (cond
    ((and *nvk-surf-handle* (> (strlen *nvk-surf-handle*) 0))
     (princ (strcat "\n[НВК] TIN L0 с панели: "
                    (if *nvk-surf-name* *nvk-surf-name* *nvk-surf-handle*)))
     T)
    ((not *nvk-dll-ok*)
     (princ "\n[НВК] DLL нет — TIN пропустим.")
     nil)
    (t
     (setq lst (vl-catch-all-apply 'nvk-surfaces nil))
     (cond
       ((or (vl-catch-all-error-p lst) (null lst))
        (princ "\n[НВК] поверхностей нет.")
        nil)
       ((= (length lst) 1)
        (setq *nvk-surf-handle* (car (car lst))
              *nvk-surf-name*   (cadr (car lst)))
        (princ (strcat "\n[НВК] TIN: " *nvk-surf-name*))
        T)
       (t
        (princ "\n[НВК] TIN задай на панели (Покрытия → L0).")
        nil)))))

(defun nvk-stage-domain ( / dump)
  (if (null *nvk-pipe-ss*)
    (nvk-stage-input))
  (if (null *nvk-pipe-ss*)
    nil
    (progn
      (sssetfirst nil *nvk-pipe-ss*)
      (setq dump
        (vl-catch-all-apply 'nvk-domain
          (list *nvk-stations-str*
                *nvk-bedding*
                *nvk-thk-zash*
                *nvk-joint*
                (if *nvk-surf-handle* *nvk-surf-handle* "")
                *nvk-cs-hwidth*
                *nvk-cs-slope*
                *nvk-thk-asph*
                (if *nvk-thk-gazn* *nvk-thk-gazn* 0.0)
                (if *nvk-thk-trot* *nvk-thk-trot* 0.0))))
      (sssetfirst nil nil)
      (if (vl-catch-all-error-p dump)
        (progn (princ (strcat "\n[НВК] домен: " (vl-catch-all-error-message dump))) nil)
        (progn
          (setq *nvk-last-dump* dump)
          T)))))

(defun nvk-info-dll (key / r)
  (setq r (vl-catch-all-apply 'nvk-domain-info (list key)))
  (if (or (vl-catch-all-error-p r) (null r)) "" r))

(defun nvk-stage-chain ( / )
  (if (not (nvk-stage-domain))
    nil
    (progn
      (setq *nvk-seg-id* (nvk-info-dll "SEG"))
      (nvk-info-dll "CHAIN"))))

(defun nvk-pick-type (label tag col / acc pt1 pt2 ch1 ch2 done tmp)
  (setq acc ""  done nil)
  (princ (strcat "\n[НВК] " label " — пары точек на плане. Enter — следующий тип."))
  (while (not done)
    (setq pt1 (getpoint (strcat "\n  " label " начало <Enter=стоп>: ")))
    (if (null pt1)
      (setq done T)
      (progn
        (setq pt2 (getpoint pt1 (strcat "\n  " label " конец: ")))
        (if pt2
          (progn
            (setq ch1 (vl-catch-all-apply 'nvk-xy-to-ch (list (car pt1) (cadr pt1)))
                  ch2 (vl-catch-all-apply 'nvk-xy-to-ch (list (car pt2) (cadr pt2))))
            (if (or (vl-catch-all-error-p ch1) (vl-catch-all-error-p ch2)
                    (not (numberp ch1)) (not (numberp ch2)) (< ch1 0) (< ch2 0))
              (princ "\n  [НВК] точка не на оси труб — кликни ближе к сети.")
              (progn
                (if (> ch1 ch2)
                  (setq tmp ch1  ch1 ch2  ch2 tmp))
                (setq acc (strcat acc (if (= acc "") "" " ")
                                  tag ":" (rtos ch1 2 3) "-" (rtos ch2 2 3)))
                (princ (strcat "\n  + " tag " ПК " (rtos ch1 2 2) "—" (rtos ch2 2 2))))))))))
  acc)

(defun nvk-pick-zones-run ( / a g tr o)
  (setq a (nvk-pick-type "АСФАЛЬТ" "А" 1)
        g (nvk-pick-type "ГАЗОН" "Г" 3)
        tr (nvk-pick-type "ТРОТУАР" "Т" 4)
        o (nvk-pick-type "ПРОЧЕЕ" "П" 8)
        *nvk-zones-str* (vl-string-trim " " (strcat a " " g " " tr " " o))
        *nvk-pick-zones* nil)
  (vl-catch-all-apply 'nvk-ui-otdat nil)
  (princ (strcat "\n[НВК] зоны: " (if (> (strlen *nvk-zones-str*) 0) *nvk-zones-str* "(нет)")))
  *nvk-zones-str*)

;; Команда НВК-ЗОНЫ — CommandMethod в DLL (ZonePicker). Lisp не переопределяет.

(defun C:НВК-ЧЕРНАЯ-ЗЕМЛЯ ( / lst i pair idx s)
  (if (not (nvk-dll-zagruzit))
    (princ "\n[НВК] без DLL.")
    (progn
      (setq lst (vl-catch-all-apply 'nvk-surfaces nil))
      (if (or (vl-catch-all-error-p lst) (null lst))
        (princ "\n[НВК] поверхностей нет.")
        (progn
          (princ "\n[НВК] TIN черной земли:")
          (setq i 0)
          (foreach pair lst
            (setq i (1+ i))
            (princ (strcat "\n  " (itoa i) "  " (cadr pair))))
          (setq s (getstring (strcat "\n[НВК] номер <1.." (itoa (length lst)) ">: ")))
          (setq idx (atoi s))
          (if (and (>= idx 1) (<= idx (length lst)))
            (progn
              (setq pair (nth (1- idx) lst)
                    *nvk-black-earth-h* (car pair)
                    *nvk-black-earth-n* (cadr pair)
                    *nvk-use-black-earth* T)
              (vl-catch-all-apply 'nvk-ui-otdat nil)
              (princ (strcat "\n[НВК] черная земля: " *nvk-black-earth-n*)))
            (princ "\n[НВК] отмена."))))))
  (princ))

(defun C:НВК-РАНДОМ ( / )
  (vl-catch-all-apply 'nvk-fresh-scatter (list 0))
  (princ "\n[НВК] новый набор случайных отклонений по всем уровням.")
  (if (nvk-dll-est-p) (nvk-progon))
  (princ))

(defun C:НВК-L1-СОХРАНИТЬ ( / n r)
  (setq n (getstring T "\n[НВК] имя пресета L1 <default>: "))
  (if (or (null n) (= (vl-string-trim " " n) "")) (setq n "default"))
  (setq *nvk-l1-preset* n)
  (vl-catch-all-apply 'nvk-ui-otdat nil)
  (setq r (vl-catch-all-apply 'nvk-l1-save (list n)))
  (princ (strcat "\n[НВК] " (if (vl-catch-all-error-p r) (vl-catch-all-error-message r) (vl-princ-to-string r))))
  (princ))

(defun C:НВК-L1-ЗАГРУЗИТЬ ( / n r)
  (setq n (getstring T "\n[НВК] имя пресета L1 <default>: "))
  (if (or (null n) (= (vl-string-trim " " n) "")) (setq n "default"))
  (setq r (vl-catch-all-apply 'nvk-l1-load (list n)))
  (if (vl-catch-all-error-p r)
    (princ (strcat "\n[НВК] " (vl-catch-all-error-message r)))
    (progn
      (setq *nvk-l1-preset* n)
      (princ (strcat "\n[НВК] " (vl-princ-to-string r)))))
  (princ))

(defun nvk-stage-stations ( / s)
  (setq s (nvk-info-dll "STATIONS"))
  (if (= s "") nil s))

(defun nvk-stage-surf ( / s)
  (setq s (nvk-info-dll "SURF"))
  (if (= s "") nil s))

(defun nvk-stage-geom-cs ( / )
  (if (and *nvk-last-dump* (eq (type *nvk-last-dump*) 'STR) (> (strlen *nvk-last-dump*) 0))
    "стек линий — лог CHAIN"
    (if (nvk-dll-est-p) "домен есть" nil)))

(princ "\n  nvk-input.lsp")
(princ)
