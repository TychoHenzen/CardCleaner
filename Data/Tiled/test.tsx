<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.2" name="test" tilewidth="8" tileheight="8" tilecount="960" columns="32" tilerendersize="grid" fillmode="preserve-aspect-fit">
 <editorsettings>
  <export target="test..tsj" format="json"/>
 </editorsettings>
 <image source="../../Graphics/Terrain/7souls/A2_autotile_sheet.png" width="256" height="240"/>
 <wangsets>
  <wangset name="Grass3" class="Grass3" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="466" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="584" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="585" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="586" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="587" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="616" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="617" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="618" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="619" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="648" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="649" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="650" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="651" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="680" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="681" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="682" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="683" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="712" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="713" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="714" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="715" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="744" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="745" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="746" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="747" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="solid"/>
   </properties>
  </wangset>
  <wangset name="Grass2" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="580" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="581" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="582" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="583" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="612" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="613" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="614" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="615" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="644" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="645" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="646" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="647" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="676" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="677" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="678" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="679" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="708" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="709" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="710" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="711" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="740" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="741" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="742" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="743" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Grass1" type="corner" tile="497">
   <wangcolor name="Background" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="195" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="576" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="577" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="578" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="579" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="608" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="609" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="610" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="611" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="640" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="641" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="642" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="643" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="672" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="673" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="674" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="675" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="704" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="705" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="706" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="707" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="736" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="737" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="738" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="739" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
 </wangsets>
</tileset>
