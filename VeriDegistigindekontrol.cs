using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Layout;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using Sirketiz.Module.BusinessObjects.Sirket_izDB;
using System;
using System.Security.AccessControl;

namespace SentezPdks.Blazor.Server.Controllers
{
    public partial class VeriDegistigindekontrol : ViewController
    {
        private bool hasChanges;
        private string propertyName;

        public VeriDegistigindekontrol()
        {
            InitializeComponent();
            TargetViewType = ViewType.Any;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            ObjectSpace.ObjectChanged += ObjectSpace_ObjectChanged;
            ObjectSpace.Committed += ObjectSpace_Committed;
        }

        protected override void OnDeactivated()
        {
            ObjectSpace.ObjectChanged -= ObjectSpace_ObjectChanged;
            ObjectSpace.Committed -= ObjectSpace_Committed;
            base.OnDeactivated();
        }

        private void ObjectSpace_ObjectChanged(object sender, ObjectChangedEventArgs e)
        {
            if (hasChanges) return;




            if (View.CurrentObject is kisi_maas kisiMaas)
            {
                hasChanges = true;
                propertyName = e.PropertyName;

                decimal gunsaat = Decimal.Parse((kisiMaas.kisi_kartlari_to.hesapkodid.AylikPuantaj.GunKacSaat.TotalMinutes/60).ToString());
                kisiMaas.GunlukUcret = kisiMaas.AylikUcret / 30;
                kisiMaas.SaatlikUcret = decimal.Parse(((kisiMaas.GunlukUcret / gunsaat)).ToString());

                var session = ((XPObjectSpace)ObjectSpace).Session;
                var maasQuery = new XPQuery<kisi_maas>(session);
                var grscksQuery = new XPQuery<kisi_grscks>(session);

                var sortedMaasList = maasQuery.Where(m => m.kisi_kartlari_to == kisiMaas.kisi_kartlari_to && m.tarih.Date == kisiMaas.tarih)
                                              .OrderBy(m => m.tarih)
                                              .ToList().FirstOrDefault();
                if (sortedMaasList != null)
                {
                    var sontarih = maasQuery.Where(m => m.kisi_kartlari_to == kisiMaas.kisi_kartlari_to && m.tarih.Date > sortedMaasList.tarih)
                                       .OrderBy(m => m.tarih).ToList().FirstOrDefault();



                    var currentMaas = sortedMaasList;
                    DateTime startDate = currentMaas.tarih;
                    DateTime endDate = sortedMaasList.tarih < sontarih.tarih ? sontarih.tarih : DateTime.MaxValue;

                    var grscksList = grscksQuery.Where(gr => gr.kisi_kartlari_to == currentMaas.kisi_kartlari_to && gr.tarih >= startDate && gr.tarih < endDate).ToList();

                    foreach (var grscks in grscksList)
                    {
                        decimal den = currentMaas.SaatlikUcret * (decimal)(grscks.Normal_Mesai?.TotalHours ?? 0);
                        grscks.Normal_M_Ucret = den;
                    }

                }
                // We will reset the hasChanges state here to avoid triggering the change when we change the grscks.Normal_M_Ucret
                hasChanges = false;

                ObjectSpace.CommitChanges();
            }
            if (View.CurrentObject is kisi_grscks kisigrscks  && kisigrscks.giris_saat != null && kisigrscks.tarih != null)
            {   // Puantaj Gün Balangıç--************************************
                if (propertyName == "giris_saat" ||propertyName == "tarih" || kisigrscks.giris_saat != null && kisigrscks.tarih != null)
                {

                    var plndty = kisigrscks.kisi_kartlari_to.hesapkodid.plan_genel_detays.Where(x => x.tarih == kisigrscks.tarih && x.IsDeleted == false).FirstOrDefault();
                    if (plndty != null)
                    {
                        var pntj = plndty.vardiya_id.vardiya_detays.Where(x => x.IsDeleted == false);
                        var pntjgece = plndty.vardiya_id.vardiya_detays.Where(x => x.baslangic > x.bitis && x.IsDeleted == false).FirstOrDefault();
                        if (pntj != null)
                        {
                            foreach (var item in pntj)
                            {
                                DateTime xbaslangic = DateTime.Parse(item.baslangic.ToString());
                                DateTime xbitis = DateTime.Parse(item.bitis.ToString());
                                DateTime xgiris = DateTime.Parse(kisigrscks.giris_saat.ToString());
                                if (xgiris < xbaslangic)
                                {
                                    xgiris = xgiris.AddDays(1);
                                }
                                if (xbaslangic > xbitis)
                                {

                                    xbitis = DateTime.Parse(pntjgece.bitis.ToString()).AddDays(1);
                                }
                                if (xgiris >= xbaslangic && xgiris < xbitis)
                                {
                                    kisigrscks.PuantajGun = item.puantajgun_id;
                                }
                            }

                        }

                    }
                }
                // Puantaj Gün Balangıç Bitiş --*****************************
                if (kisigrscks.giris_saat != null && kisigrscks.cikis_saat != null && kisigrscks.PuantajGun != null)
                {


                    double grssaat = TimeSpan.Parse(kisigrscks.giris_saat.ToString()).TotalMinutes;
                    double ckssaat = TimeSpan.Parse(kisigrscks.cikis_saat.ToString()).TotalMinutes;
                    double ogrssaat = TimeSpan.Parse(kisigrscks.giris_saat.ToString()).TotalMinutes;
                    double ockssaat = TimeSpan.Parse(kisigrscks.cikis_saat.ToString()).TotalMinutes;


                    if (grssaat > ckssaat)
                    {
                        ckssaat = ckssaat + 1440;
                    }

                    double pngbasla;
                    double pngbitis;
                    double mlgrs;
                    double mlcks;
                    double gn_dnm;
                    double molatoplam = 0;
                    double fmolatoplam = 0;
                    double ffmolatoplam = 0;
                    double NormalCalismaUcret = 0;
                    double FazlaCalismaUcret = 0;
                    double FFazlaCalismaUcret = 0;
                    double ftplml = 0;
                    double fftplml = 0;
                    double toplam = 0;
                    double fMesai = 0;
                    double ffMesai = 0;
                    double pngmin = 0;
                    double gecgelme = 0;
                    double pbasla = 0;
                    double pbitis = 0;
                    double erkencikma = 0;
                    double pmaxc = 0;
                    double pngcks = 0;
                    double pnggrs = 0;
                    gn_dnm = TimeSpan.Parse(kisigrscks.PuantajGun.gun_donum.ToString()).TotalMinutes;
                    gecgelme = TimeSpan.Parse(kisigrscks.PuantajGun.gec_gelme.ToString()).TotalMinutes;
                    erkencikma = TimeSpan.Parse(kisigrscks.PuantajGun.erken_cikma.ToString()).TotalMinutes;
                    pbasla = TimeSpan.Parse(kisigrscks.PuantajGun.giris_saat.ToString()).TotalMinutes;
                    pbitis = ckssaat;
                    pngcks = TimeSpan.Parse(kisigrscks.PuantajGun.cikis_saat.ToString()).TotalMinutes;
                    pmaxc = TimeSpan.Parse(kisigrscks.PuantajGun.gunkacsaat.ToString()).TotalMinutes;
                    if (kisigrscks.PuantajGun.cikis_saat.ToString() == "00:00:00")
                    {
                        pngcks = 1440;
                    }
                    if (kisigrscks.PuantajGun.giris_saat.ToString() == "00:00:00")
                    {
                        pnggrs = 1440;
                    }
                    if (ckssaat >= erkencikma && ckssaat <= pngcks)
                    {

                        ckssaat = pngcks;
                        ockssaat = pngcks;
                    }
                    if (grssaat >= pbasla && grssaat < gecgelme)
                    {
                        grssaat = pbasla;
                    }
                    if (pbasla == 0)
                    {
                        pbasla = pbasla + 1440;
                    }
                    if (pbitis == 0)
                    {
                        pbitis = pbitis + 1440;
                    }
                    if (ckssaat < gn_dnm)
                    {
                        ckssaat = ckssaat + 1440;
                        ockssaat = ockssaat + 1440;
                    }
                    if (grssaat < gn_dnm)
                    {

                        grssaat = grssaat + 1440;
                    }
                    kisigrscks.ToplamCalısmaSaati = string.Format("{0:hh\\:mm}", TimeSpan.FromMinutes((ckssaat - grssaat))).ToString();
                    foreach (var pngd in kisigrscks.PuantajGun.puantaj_gun_detays)
                    {
                        var bodro = pngd.bordroid;
                        //if (cikis_saat < giris_saat)
                        //    ckssaat = TimeSpan.Parse(cikis_saat.ToString()).TotalMinutes + 1440;

                        pngbasla = TimeSpan.Parse(pngd.basla.ToString()).TotalMinutes;
                        pngbitis = TimeSpan.Parse(pngd.bitis.ToString()).TotalMinutes;
                        if (pngbasla == 0)
                        {
                            pngbasla = pngbasla + 1440;
                        }


                        if (pngbitis == 0)
                        {
                            pngbitis = pngbitis + 1440;
                        }
                        if (pngbitis <= pngbasla)
                        {
                            pngbitis = pngbitis + 1440;
                        }
                        if (pngbitis <= gn_dnm)
                        {
                            pngbitis = pngbitis + 1440;
                        }
                        pngmin = TimeSpan.Parse(pngd.min.ToString()).TotalMinutes;

                        double cks = kisigrscks.cikis_saat.Value.TotalMinutes;
                        double cksmin = pngbitis+pngmin;

                        if (bodro.Oid == 1)
                        {
                            
                           

                            if (kisigrscks.PuantajGun.gec_gelme < kisigrscks.giris_saat)
                            {
                                if (kisigrscks.giris_saat > kisigrscks.PuantajGun.giris_saat)
                                {
                                    kisigrscks.GecGelme = kisigrscks.giris_saat - kisigrscks.PuantajGun.giris_saat;
                                }
                                else
                                {
                                    kisigrscks.GecGelme = null;
                                }
                                
                            }
                            else
                                kisigrscks.GecGelme = null;

                            if (kisigrscks.PuantajGun.erken_cikma > kisigrscks.cikis_saat && kisigrscks.cikis_saat > kisigrscks.giris_saat)
                            {
                                if (kisigrscks.PuantajGun.cikis_saat > kisigrscks.cikis_saat)
                                {
                                    kisigrscks.ErkenCikma = kisigrscks.PuantajGun.cikis_saat - kisigrscks.cikis_saat;
                                }
                                else
                                {
                                    kisigrscks.ErkenCikma = null;
                                }
                            }
                            else
                                kisigrscks.ErkenCikma = null;


                            if (ogrssaat < gecgelme && ogrssaat > pbasla)
                            {
                                grssaat = pbasla;
                            }
                            if (ockssaat < erkencikma && ockssaat > pbitis)
                            {
                                ckssaat = pbitis;
                            }
                            if (ckssaat >= (pngbitis) && ckssaat < (pngbitis + pngmin))
                            {
                                ckssaat = pngbitis;
                            }


                            if (ogrssaat >= pngbasla && ogrssaat < pngbitis)
                            {
                                pngbasla = grssaat;
                            }

                            if (ockssaat <= pngbitis && ockssaat >= pngbasla)
                            {
                                pngbitis = ckssaat;
                            }

                            if (grssaat >= pngbasla && grssaat <= pngbitis)
                            {
                                toplam = toplam + (pngbitis - grssaat);



                            }
                            else if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat >= pngbitis)
                            {
                                toplam = toplam + (pngbitis - pngbasla);




                            }
                            else if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat <= pngbitis)
                            {
                                toplam = toplam + (ckssaat - pngbasla);




                            }
                            foreach (var ml in pngd.Puantaj_molas)
                            {
                                mlgrs = TimeSpan.Parse(ml.giris.ToString()).TotalMinutes;
                                mlcks = TimeSpan.Parse(ml.cikis.ToString()).TotalMinutes;

                                if (kisigrscks.PuantajGun.giris_saat < ml.giris)
                                {


                                    if (grssaat >= mlgrs && grssaat <= mlcks)
                                    {
                                        molatoplam = molatoplam + (mlcks - grssaat);

                                    }
                                    if (grssaat < mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat >= mlcks)
                                    {
                                        molatoplam = molatoplam + (mlcks - mlgrs);
                                    }
                                    if (grssaat <= mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat <= mlcks)
                                    {
                                        molatoplam = molatoplam + (ckssaat - mlgrs);
                                    }
                                }

                            }
                            try
                            {
                                if (kisigrscks?.kisi_kartlari_to?.kisi_maass?.Count > 0)
                                {
                                    var lastKisiMaas = kisigrscks.kisi_kartlari_to.kisi_maass
                                        .Where(x => x.tarih <= kisigrscks.tarih)
                                        .LastOrDefault();

                                    if (lastKisiMaas != null)
                                    {
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)0)
                                        {
                                            double ucrt = double.Parse(lastKisiMaas.SaatlikUcret.ToString());
                                            NormalCalismaUcret += (ucrt * (pngd.carpan / 100));
                                        }
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)1)
                                        {
                                            double ucrt = double.Parse(lastKisiMaas.SaatlikUcret2.ToString());
                                            NormalCalismaUcret += (ucrt * (pngd.carpan / 100));
                                        }
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)2)
                                        {
                                            double ucrt = double.Parse(lastKisiMaas.SaatlikUcret3.ToString());
                                            NormalCalismaUcret += (toplam - molatoplam) * (ucrt * (pngd.carpan / 100));
                                        }
                                    }
                                }
                               
                            }
                            catch (Exception)
                            {
                                // Handle the exception properly here
                            }

                        }

                        if (bodro.Oid == 2)
                        {
                            //grssaat = pbitis;

                            foreach (var ml in pngd.Puantaj_molas)
                            {
                                mlgrs = TimeSpan.Parse(ml.giris.ToString()).TotalMinutes;
                                mlcks = TimeSpan.Parse(ml.cikis.ToString()).TotalMinutes;

                                if (kisigrscks.PuantajGun.giris_saat < ml.giris)
                                {


                                    if (grssaat >= mlgrs && grssaat <= mlcks)
                                    {
                                        fmolatoplam = fmolatoplam + (mlcks - grssaat);
                                        ftplml = (mlcks - grssaat);
                                    }
                                    if (grssaat < mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat >= mlcks)
                                    {
                                        fmolatoplam = fmolatoplam + (mlcks - mlgrs);
                                        ftplml = (mlcks - mlgrs);
                                    }
                                    if (grssaat <= mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat <= mlcks)
                                    {
                                        fmolatoplam = fmolatoplam + (ckssaat - mlgrs);
                                        ftplml = (ckssaat - mlgrs);
                                    }
                                }

                            }
                            pngbasla = TimeSpan.Parse(pngd.basla.ToString()).TotalMinutes;

                            pngbitis = TimeSpan.Parse(pngd.bitis.ToString()).TotalMinutes;

                            if (gn_dnm >= pngbasla)
                            {
                                pngbasla = pngbasla + 1440;
                                pbasla = pbasla + 1440;
                            }
                            if (gn_dnm >= pngbitis)
                            {
                                pngbitis = pngbitis + 1440;
                                pbitis = pbitis + 1440;
                            }
                            if (ckssaat > pngbitis && ckssaat <= pngbitis + pngmin)
                            {
                                ckssaat = pngbitis;
                            }

                            if (ckssaat <= pngbitis && ckssaat >= pngbasla)
                            {
                                pngbitis = ckssaat;
                            }

                            if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat >= pngbitis && ckssaat != pngbasla)
                            {
                                try
                                {

                                    if (kisigrscks.kisi_kartlari_to.kisi_maass.Count > 0)
                                    {
                                        fMesai += (pngbitis - pngbasla);
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)0)
                                        {
                                            double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret.ToString());
                                            double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                            FazlaCalismaUcret += (((pngbitis - pngbasla) - ftplml) * ucretcarpan) / 60;
                                        }
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)1)
                                        {
                                            double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret2.ToString());
                                            double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                            FazlaCalismaUcret += (((pngbitis - pngbasla) - ftplml) * ucretcarpan) / 60;
                                        }
                                        if (pngd.ucret == (puantaj_gun_detay.UcretSec)2)
                                        {
                                            double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret3.ToString());
                                            double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                            FazlaCalismaUcret += (((pngbitis - pngbasla) - ftplml) * ucretcarpan) / 60;
                                        }
                                    }
                                }
                                catch (Exception)
                                {


                                }
                            }
                            else if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat <= pngbitis && ckssaat != pngbasla)
                            {
                                fMesai += (ckssaat - pngbasla);
                                TimeSpan.FromMinutes(ckssaat);
                                TimeSpan.FromMinutes(pngbasla);

                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)0)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FazlaCalismaUcret += ((fMesai - ftplml) * ucretcarpan) / 60;
                                }
                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)1)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret2.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FazlaCalismaUcret += ((fMesai - ftplml) * ucretcarpan) / 60;
                                }
                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)2)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret3.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FazlaCalismaUcret += ((fMesai - ftplml) * ucretcarpan) / 60;
                                }
                            }

                        }
                        if (bodro.Oid == 3)
                        {
                            //grssaat = TimeSpan.Parse( giris_saat.ToString()).TotalMinutes;
                            //ckssaat = TimeSpan.Parse( cikis_saat.ToString()).TotalMinutes;
                            pngbasla = TimeSpan.Parse(pngd.basla.ToString()).TotalMinutes;

                            pngbitis = TimeSpan.Parse(pngd.bitis.ToString()).TotalMinutes;
                            foreach (var ml in pngd.Puantaj_molas)
                            {
                                mlgrs = TimeSpan.Parse(ml.giris.ToString()).TotalMinutes;
                                mlcks = TimeSpan.Parse(ml.cikis.ToString()).TotalMinutes;

                                if (kisigrscks.PuantajGun.giris_saat < ml.giris)
                                {


                                    if (grssaat >= mlgrs && grssaat <= mlcks)
                                    {
                                        ffmolatoplam = ffmolatoplam + (mlcks - grssaat);
                                        fftplml = (mlcks - grssaat);
                                    }
                                    if (grssaat < mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat >= mlcks)
                                    {
                                        ffmolatoplam = ffmolatoplam + (mlcks - mlgrs);
                                        fftplml = (mlcks - mlgrs);
                                    }
                                    if (grssaat <= mlgrs && grssaat <= mlcks && ckssaat >= mlgrs && ckssaat <= mlcks)
                                    {
                                        ffmolatoplam = ffmolatoplam + (ckssaat - mlgrs);
                                        fftplml = (ckssaat - mlgrs);
                                    }
                                }

                            }
                            if (ckssaat > pngbitis && ckssaat < pngbitis + pngmin)
                            {
                                ckssaat = pngbitis;
                            }

                            if (ckssaat <= pngbitis && ckssaat > pngbasla)
                            {
                                pngbitis = ckssaat;
                            }

                            if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat > pngbitis && ckssaat != pngbasla)
                            {
                                try
                                {


                                    ffMesai += (pngbitis - pngbasla);
                                    if (pngd.ucret == (puantaj_gun_detay.UcretSec)0)
                                    {
                                        double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret.ToString());
                                        double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                        FFazlaCalismaUcret += (((pngbitis - pngbasla) - fftplml) * ucretcarpan) / 60;
                                    }
                                    if (pngd.ucret == (puantaj_gun_detay.UcretSec)1)
                                    {
                                        double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret2.ToString());
                                        double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                        FFazlaCalismaUcret += (((pngbitis - pngbasla) - fftplml) * ucretcarpan) / 60;
                                    }
                                    if (pngd.ucret == (puantaj_gun_detay.UcretSec)2)
                                    {
                                        double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret3.ToString());
                                        double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                        FFazlaCalismaUcret += (((pngbitis - pngbasla) - fftplml) * ucretcarpan) / 60;
                                    }
                                }
                                catch (Exception)
                                {


                                }
                            }
                            else if (grssaat <= pngbasla && grssaat <= pngbitis && ckssaat >= pngbasla && ckssaat <= pngbitis && ckssaat != pngbasla)
                            {
                                ffMesai += (ckssaat - pngbasla);
                                TimeSpan.FromMinutes(ckssaat);
                                TimeSpan.FromMinutes(pngbasla);

                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)0)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FFazlaCalismaUcret += ((ffMesai - fftplml) * ucretcarpan) / 60;
                                }
                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)1)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret2.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FFazlaCalismaUcret += ((ffMesai - fftplml) * ucretcarpan) / 60;
                                }
                                if (pngd.ucret == (puantaj_gun_detay.UcretSec)2)
                                {
                                    double ucrt = double.Parse(kisigrscks.kisi_kartlari_to.kisi_maass.Where(x => x.tarih <= kisigrscks.tarih).LastOrDefault().SaatlikUcret3.ToString());
                                    double ucretcarpan = (ucrt * (double.Parse(pngd.carpan.ToString()) / 100));
                                    FFazlaCalismaUcret += ((ffMesai - fftplml) * ucretcarpan) / 60;
                                }
                            }

                        }
                        if (cksmin>=cks)
                        {
                            kisigrscks.Fazla_M_1 = TimeSpan.FromMinutes(0);
                            hasChanges = true;
                        }
                    }
                    toplam = toplam - molatoplam;
                    if (toplam > pmaxc)
                    {
                        toplam = pmaxc;
                    }

                    kisigrscks.Normal_Mesai = TimeSpan.FromMinutes(toplam);
                    kisigrscks.Eksik_Mesai = TimeSpan.Parse(kisigrscks.PuantajGun.gunkacsaat.ToString()) - TimeSpan.Parse(kisigrscks.Normal_Mesai.ToString());
                    if (kisigrscks.Eksik_Mesai < TimeSpan.Zero)
                    {
                        kisigrscks.Eksik_Mesai = TimeSpan.Zero;
                    }

                    if (fMesai - fmolatoplam > 0)
                    {
                        kisigrscks.Fazla_M_1 = TimeSpan.FromMinutes(fMesai - fmolatoplam);
                    }
                    //else
                    //{
                    //     Fazla_M_1 = TimeSpan.FromMinutes(0);
                    //}
                    if (ffMesai - ffmolatoplam > 0)
                    {
                        kisigrscks.Fazla_M_2 = TimeSpan.FromMinutes(ffMesai - ffmolatoplam);
                    }
                    else
                    {
                        kisigrscks.Fazla_M_2 = TimeSpan.FromMinutes(0);
                    }


                    kisigrscks.Normal_M_Ucret = decimal.Parse(NormalCalismaUcret.ToString()) * decimal.Parse(((toplam) / 60).ToString());
                    kisigrscks.Fazla_M_1_Ucret = decimal.Parse(FazlaCalismaUcret.ToString());
                    kisigrscks.Fazla_M_2_Ucret = decimal.Parse(FFazlaCalismaUcret.ToString());


                    kisigrscks.ToplamUcret = kisigrscks.Normal_M_Ucret + kisigrscks.Fazla_M_1_Ucret + kisigrscks.Fazla_M_2_Ucret;

                }
                if (kisigrscks.kisi_kartlari_to.hesapkodid.plan_genel_detays.Count > 0)
                {
                    var genel_detay = kisigrscks.kisi_kartlari_to.hesapkodid.plan_genel_detays.Where(x => x.tarih.Date == kisigrscks.tarih.Date).FirstOrDefault();

                    if (genel_detay != null)
                    {
                        var vardiya_id = genel_detay.vardiya_id;

                        if (vardiya_id != null)
                        {
                            var vardiya_detay = vardiya_id.vardiya_detays.Where(x => x.tatil_id != null).FirstOrDefault();

                            if (vardiya_detay != null)
                            {
                                kisigrscks.Normal_Mesai = kisigrscks.PuantajGun.gunkacsaat;

                                // Bu noktada, hftatili artık güvenli bir şekilde kullanılabilir
                            }
                        }
                    }

                    //var hftatili = kisi_kartlari_to.hesapkodid.plan_genel_detays.Where(x => x.tarih.Date == tarih.Date).FirstOrDefault().vardiya_id.vardiya_detays.Where(x => x.tatil_id != null).FirstOrDefault();
                    //if (hftatili != null)
                    //{
                    //Normal_Mesai = PuantajGun.gunkacsaat;
                    //}
                }

                hasChanges=true;

            }
            if (e.Object is kisi_grscks kisig && (e.OldValue == null || e.PropertyName == "IsDeleted")&&!hasChanges&& kisig.cikis_saat!=null)
            {
                ObjectSpace.CommitChanges();
                UpdateNormalMesaiTotalsForMonth(kisig.tarih.Year, kisig.tarih.Month, kisig.kisi_kartlari_to);
            }

           
        }


        public void UpdateNormalMesaiTotalsForMonth(int year, int month, kisi_kartlari kisi_kartlari_to)
        {
            if (kisi_kartlari_to == null)
            {
                throw new ArgumentNullException(nameof(kisi_kartlari_to));
            }
            Session session = ((XPObjectSpace)ObjectSpace).Session;
            XPQuery<kisi_grscks> kisi_grscksQuery = session.Query<kisi_grscks>();
            XPQuery<KisiAyToplam> kisiAyToplamQuery = session.Query<KisiAyToplam>();

            // Get the records for the specified month and year and for the specified person
            var recordsForMonth = kisi_grscksQuery.Where(record => record.tarih.Year == year &&
                                                                   record.tarih.Month == month &&
                                                                   record.kisi_kartlari_to == kisi_kartlari_to).ToList();

            recordsForMonth = recordsForMonth.Where(record => record.IsDeleted == false).ToList();

            // Calculate the total Normal_Mesai for the month
            double NM_Top_Ay = 0;
            decimal NM_Top_Ay_Ucrt = 0;
            double EKM_Top_Ay = 0;
            decimal EKM_Top_Ay_Ucrt = 0;
            double FZLM_1_Top_Ay = 0;
            decimal FZLM_1_Top_Ay_Ucrt = 0;
            double FZLM_2_Top_Ay = 0;
            decimal FZLM_2_Top_Ay_Ucrt = 0;
            foreach (var record in recordsForMonth)
            {
                if (record.Normal_Mesai != null)
                {
                    NM_Top_Ay += record.Normal_Mesai.Value.TotalHours;
                    NM_Top_Ay_Ucrt+=record.Normal_M_Ucret;
                }
                if (record.Eksik_Mesai != null)
                {
                    EKM_Top_Ay += record.Eksik_Mesai.Value.TotalHours;
                    EKM_Top_Ay_Ucrt+=record.EksikMesaiUcret;
                }
                if (record.Fazla_M_1 != null)
                {
                    FZLM_1_Top_Ay += record.Fazla_M_1.Value.TotalHours;
                    FZLM_1_Top_Ay_Ucrt+=record.Fazla_M_1_Ucret;
                }
                if (record.Fazla_M_2 != null)
                {
                    FZLM_2_Top_Ay += record.Fazla_M_2.Value.TotalHours;
                    FZLM_2_Top_Ay_Ucrt+=record.Fazla_M_2_Ucret;
                }
            }

            // Convert total hours to the format: hours:minutes
            int NMhours = (int)NM_Top_Ay;
            int NMminutes = (int)((NM_Top_Ay - NMhours) * 60);
            string Top_NormalMesaiS = $"{NMhours}:{NMminutes:00}";

            int EKhours = (int)EKM_Top_Ay;
            int EKminutes = (int)((EKM_Top_Ay - EKhours) * 60);
            string Top_EksikMesai = $"{EKhours}:{EKminutes:00}";

            int FZL_1_hours = (int)FZLM_1_Top_Ay;
            int FZL_1_minutes = (int)((FZLM_1_Top_Ay - FZL_1_hours) * 60);
            string Top_FZL_1_Mesai = $"{FZL_1_hours}:{FZL_1_minutes:00}";

            int FZL_2_hours = (int)FZLM_2_Top_Ay;
            int FZL_2_minutes = (int)((FZLM_2_Top_Ay - FZL_2_hours) * 60);
            string Top_FZL_2_Mesai = $"{FZL_2_hours}:{FZL_2_minutes:00}";

            // Find the KisiAyToplam record for the month and year and for the specified person
            var kisiAyToplamForMonth = kisiAyToplamQuery.FirstOrDefault(record => record.Tarih.Year == year &&
                                                                                 record.Tarih.Month == month &&
                                                                                 record.kisikartlari == kisi_kartlari_to);

            if (kisiAyToplamForMonth == null || kisiAyToplamForMonth.IsDeleted)
            {
                // If the KisiAyToplam record doesn't exist or is deleted, create it
                kisiAyToplamForMonth = new KisiAyToplam(session)
                {
                    Tarih = new DateTime(year, month, 1),
                    kisikartlari = kisi_kartlari_to,
                    NormalMesai = Top_NormalMesaiS,
                    NM_Ucret = NM_Top_Ay_Ucrt,
                    Eksik_Mesai=Top_EksikMesai,
                    Eksik_M_Ucret=EKM_Top_Ay_Ucrt,
                    Fazla_M_1=Top_FZL_1_Mesai,
                    Fazla_M_1_Ucret=FZLM_1_Top_Ay_Ucrt,
                    Fazla_M_2=Top_FZL_2_Mesai,
                    Fazla_M_2_Ucret=FZLM_2_Top_Ay_Ucrt

                };
            }
            else
            {
                // Update the NormalMesai field with the total calculated from the kisi_grscks records
                kisiAyToplamForMonth.NormalMesai = Top_NormalMesaiS;
                kisiAyToplamForMonth.NM_Ucret= NM_Top_Ay_Ucrt;
                kisiAyToplamForMonth.Eksik_Mesai=Top_EksikMesai;
                kisiAyToplamForMonth.Eksik_M_Ucret=EKM_Top_Ay_Ucrt;
                kisiAyToplamForMonth.Fazla_M_1=Top_FZL_1_Mesai;
                kisiAyToplamForMonth.Fazla_M_1_Ucret=FZLM_1_Top_Ay_Ucrt;
                kisiAyToplamForMonth.Fazla_M_2=Top_FZL_2_Mesai;
                kisiAyToplamForMonth.Fazla_M_2_Ucret=FZLM_2_Top_Ay_Ucrt;
            }

            // Save the changes
            session.Save(kisiAyToplamForMonth);
            session.CommitTransaction();
            
        }

        private void ObjectSpace_Committed(object sender, EventArgs e)
        {
            if (hasChanges) return;
            if (View.CurrentObject is kisi_grscks kisigrscks)
            {


                UpdateNormalMesaiTotalsForMonth(kisigrscks.tarih.Year, kisigrscks.tarih.Month, kisigrscks.kisi_kartlari_to);




            }
        }

        private void ObjectSpace_RollingBack(object sender, EventArgs e)
        {
            hasChanges = false;
        }
    }
}
