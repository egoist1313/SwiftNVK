;;; nvk-planvol.lsp — этапы VOL и PLAN
;;; Зависит от: nvk-config, nvk-dll, nvk-input, nvk-cs, nvk-ep.

(setq *nvk-vol-ins* nil)

(defun nvk-stage-vol ( / p r)
  (cond
    ((not (nvk-dll-est-p)) nil)
    (t
     (if (null *nvk-vol-ins*)
       (cond
         (*nvk-ep-ins*
          (setq *nvk-vol-ins* (list (car *nvk-ep-ins*) (- (cadr *nvk-ep-ins*) 40.0))))
         (*nvk-cs-ins*
          (setq *nvk-vol-ins* (list (car *nvk-cs-ins*) (- (cadr *nvk-cs-ins*) 80.0))))
         (t
          (princ "\n[НВК] точка вставки таблицы объёмов")
          (setq p (getpoint "\n[НВК] VOL точка: "))
          (if p (setq *nvk-vol-ins* p)))))
     (if (null *nvk-vol-ins*)
       nil
       (progn
         (setq r
           (vl-catch-all-apply 'nvk-vol-draw
             (list (car *nvk-vol-ins*) (cadr *nvk-vol-ins*))))
         (cond
           ((vl-catch-all-error-p r) nil)
           ((null r) nil)
           (t r)))))))

(defun nvk-stage-plan ( / r)
  (cond
    ((not (nvk-dll-est-p)) nil)
    (t
     (setq r (vl-catch-all-apply 'nvk-plan-draw nil))
     (cond
       ((vl-catch-all-error-p r) nil)
       ((null r) nil)
       (t r)))))

(princ "\n  nvk-planvol.lsp")
(princ)
